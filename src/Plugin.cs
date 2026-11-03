// ---------------------------------------------------------------
//  Sortierknopf fuer Sicherheitsbehaelter und Rucksack
//
//  Das Lager hat im Spiel bereits einen Sortierknopf: das Bauteil
//  EFT.UI.DragAndDrop.GridSortPanel. Es traegt die ganze Logik in
//  sich - Nachfrage, Fortschrittsanzeige, InteractionsHandlerClass
//  .Sort und die Netzwerk-Uebertragung.
//
//  Darum wird hier NICHTS nachgebaut: ein vorhandenes GridSortPanel
//  wird geklont, in die Ansicht des Behaelters gehaengt und mit
//  Show(steuerung, behaelter) auf ihn gerichtet. Der Knopf sieht
//  dadurch aus wie der im Lager und verhaelt sich auch so.
//
//  Angehaengt wird an ContainedGridsView.Show - die Ansicht, die im
//  Ausruestungsbildschirm den Inhalt von Sicherheitsbehaelter und
//  Rucksack darstellt.
// ---------------------------------------------------------------
using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Sortierknopf
{
	[BepInPlugin("com.kaan.sortierknopf", "Sortierknopf", "1.0.0")]
	public class Plugin : BaseUnityPlugin
	{
		internal static ManualLogSource Log;

		internal static ConfigEntry<bool> An;

		internal static ConfigEntry<bool> Sicherheitsbehaelter;

		internal static ConfigEntry<bool> Rucksack;

		internal static ConfigEntry<bool> Alles;

		internal static ConfigEntry<bool> ImRaid;

		internal static ConfigEntry<float> VersatzX;

		internal static ConfigEntry<float> VersatzY;

		internal static ConfigEntry<bool> Rechts;

		internal static ConfigEntry<int> Ebene;

		internal static ConfigEntry<float> Groesse;

		private void Awake()
		{
			Log = base.Logger;
			An = base.Config.Bind("1. Allgemein", "Eingeschaltet", defaultValue: true, "");
			Sicherheitsbehaelter = base.Config.Bind("1. Allgemein", "Im Sicherheitsbehaelter", defaultValue: true, "");
			Rucksack = base.Config.Bind("1. Allgemein", "Im Rucksack", defaultValue: true, "");
			Alles = base.Config.Bind("1. Allgemein", "In jedem Behaelter", defaultValue: false,
				"Auch Taschen, Westen, Koffer und Kisten bekommen den Knopf.");
			ImRaid = base.Config.Bind("1. Allgemein", "Auch im Raid sortieren", defaultValue: true,
				"Das Spiel sperrt das Sortieren im Raid vollstaendig - auch seine\n"
				+ "eigenen Knoepfe. Ohne diese Einstellung meldet es beim Druck auf\n"
				+ "den Knopf nur \"is not appropriate for sorting\". Angehoben wird die\n"
				+ "Sperre nur fuer Behaelter, die dir selbst gehoeren.");
			VersatzX = base.Config.Bind("2. Lage", "Versatz waagerecht", -6f,
				new ConfigDescription("Von der gewaehlten Ecke aus. Bei Verankerung rechts "
					+ "sind negative Werte nach links.", new AcceptableValueRange<float>(-400f, 400f)));
			VersatzY = base.Config.Bind("2. Lage", "Versatz senkrecht", 24f,
				new ConfigDescription("Nach oben positiv.", new AcceptableValueRange<float>(-400f, 400f)));
			Rechts = base.Config.Bind("2. Lage", "Oben rechts verankern", defaultValue: true,
				"Aus = oben links.");
			Ebene = base.Config.Bind("2. Lage", "Ebene", 1,
				new ConfigDescription("An welches Element der Knopf gehaengt wird. 0 = die "
					+ "Rasteransicht selbst, 1 = eine Ebene darueber (dort sitzt die "
					+ "Ueberschrift RUCKSACK), 2 = zwei Ebenen darueber. Welche Namen es gibt, "
					+ "steht beim Setzen im Protokoll.",
					new AcceptableValueRange<int>(0, 3)));
			Groesse = base.Config.Bind("2. Lage", "Groesse", 1f,
				new ConfigDescription("1 = wie im Lager.", new AcceptableValueRange<float>(0.3f, 2f)));
			new Harmony("com.kaan.sortierknopf").PatchAll();
			Log.LogInfo("Sortierknopf geladen.");
		}
	}

	/// <summary>Markiert unsere eigenen Knoepfe, damit keine zweiten entstehen.</summary>
	internal class UnserKnopf : MonoBehaviour
	{
	}

	[HarmonyPatch]
	internal static class AnsichtPatch
	{
		// Es gibt zwei Show-Methoden: die abstrakte mit sechs Parametern und
		// die ausgefuehrte mit acht. Gesucht ist die zweite - ueber die
		// Parameterzahl statt ueber die Typen, weil einer davon
		// (FilterPanel) in keiner der eingebundenen Assemblies liegt.
		[HarmonyTargetMethod]
		private static MethodBase Ziel()
		{
			foreach (MethodInfo m in typeof(ContainedGridsView).GetMethods(
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (m.Name == "Show" && !m.IsAbstract && m.GetParameters().Length == 8)
				{
					Plugin.Log.LogInfo("Sortierknopf haengt an " + m.DeclaringType.Name + ".Show");
					return m;
				}
			}
			Plugin.Log.LogError("ContainedGridsView.Show mit acht Parametern nicht gefunden.");
			return null;
		}

		private static FieldInfo _steuerungFeld;

		private static bool _gesucht;

		[HarmonyPostfix]
		private static void Danach(ContainedGridsView __instance, CompoundItem compoundItem,
								   EFT.InventoryLogic.ItemController itemController)
		{
			try
			{
				if (!Plugin.An.Value || __instance == null || compoundItem == null)
				{
					return;
				}
				if (!Zustaendig(compoundItem))
				{
					return;
				}

				GridSortPanel vorlage = Vorlage(__instance);
				if (vorlage == null)
				{
					Plugin.Log.LogWarning("Kein GridSortPanel zum Klonen gefunden - "
						+ "einmal das Lager oeffnen, dann ist es geladen.");
					return;
				}
				Transform eltern = Elternteil(__instance.transform);
				if (eltern == null || eltern.GetComponentInChildren<UnserKnopf>() != null)
				{
					return;
				}

				// Die Steuerung ZUERST holen. Ohne sie waere der Knopf nur
				// Zierde - dann gar nicht erst einen anlegen.
				InventoryController steuerung = itemController as InventoryController;
				if (steuerung == null)
				{
					steuerung = AusFeld(__instance);
				}
				if (steuerung == null)
				{
					Plugin.Log.LogWarning("Keine Inventarsteuerung - kein Sortierknopf fuer "
						+ compoundItem.TemplateId);
					return;
				}

				GridSortPanel klon = UnityEngine.Object.Instantiate(vorlage, eltern, worldPositionStays: false);
				try
				{
					klon.gameObject.name = "SortierknopfKaan";
					klon.gameObject.AddComponent<UnserKnopf>();
					Entschaerfen(klon);
					Platzieren(klon.transform as RectTransform, vorlage.transform as RectTransform);
					klon.gameObject.SetActive(value: true);
					Anrichten(klon, steuerung, compoundItem);
					// Eigener Versuch: SetUnlockStatus greift auf ein
					// serialisiertes Feld zu, das im Klon fehlen kann.
					try
					{
						klon.SetUnlockStatus(value: true);
					}
					catch (Exception ex2)
					{
						Plugin.Log.LogWarning("SetUnlockStatus uebersprungen: " + ex2.Message);
					}
					Plugin.Log.LogInfo("Sortierknopf gesetzt an " + compoundItem.TemplateId
						+ " | Kette: " + Kette(__instance.transform));
				}
				catch (Exception ex)
				{
					// Nichts Halbfertiges stehen lassen - sonst haengen
					// verirrte Balken im Bild.
					UnityEngine.Object.Destroy(klon.gameObject);
					Plugin.Log.LogError("Sortierknopf-Aufbau gescheitert, Klon entfernt: " + ex);
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("Sortierknopf: " + ex);
			}
		}

		/// <summary>Namen der Elternelemente - zum Einstellen der Ebene.</summary>
		private static string Kette(Transform t)
		{
			string s = "";
			int i = 0;
			while (t != null && i < 4)
			{
				s += ((i > 0) ? " < " : "") + i + ":" + t.name;
				t = t.parent;
				i++;
			}
			return s;
		}

		private static FieldInfo _feldSteuerung;

		private static FieldInfo _feldGegenstand;

		private static bool _felderGesucht;

		/// <summary>
		/// Richtet den Klon auf Behaelter und Steuerung aus - OHNE Show().
		///
		/// An GridSortPanel.Show haengt der Mod ChouUn.Iof einen Postfix.
		/// Der erwartet ein Panel, das in der UI-Struktur des Spiels steckt,
		/// und greift bei unserem frisch erzeugten Klon ins Leere. Ergebnis
		/// war die Meldung "ChouUn.Iof threw an exception" im Spiel.
		///
		/// Show() selbst tut nichts weiter, als zwei Felder zu setzen - das
		/// koennen wir direkt, dann laeuft dort auch kein fremder Postfix.
		/// </summary>
		private static void Anrichten(GridSortPanel klon, InventoryController steuerung, CompoundItem item)
		{
			if (!_felderGesucht)
			{
				_felderGesucht = true;
				foreach (FieldInfo f in typeof(GridSortPanel).GetFields(
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
				{
					if (_feldSteuerung == null && typeof(InventoryController).IsAssignableFrom(f.FieldType))
					{
						_feldSteuerung = f;
					}
					else if (_feldGegenstand == null && typeof(CompoundItem).IsAssignableFrom(f.FieldType))
					{
						_feldGegenstand = f;
					}
				}
				Plugin.Log.LogInfo("Sortierknopf: Felder "
					+ ((_feldSteuerung != null) ? _feldSteuerung.Name : "?") + " / "
					+ ((_feldGegenstand != null) ? _feldGegenstand.Name : "?"));
			}
			if (_feldSteuerung != null && _feldGegenstand != null)
			{
				_feldSteuerung.SetValue(klon, steuerung);
				_feldGegenstand.SetValue(klon, item);
				return;
			}
			// Rueckfallebene. Der Rumpf von Show laeuft dabei durch, nur der
			// fremde Postfix danach kann stolpern - das ist zu verschmerzen.
			try
			{
				klon.Show(steuerung, item);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("Show() hat einen fremden Postfix gestoert: " + ex.Message);
			}
		}

		private static bool Zustaendig(CompoundItem item)
		{
			if (Plugin.Alles.Value)
			{
				return true;
			}
			try
			{
				string slot = item.CurrentAddress?.Container?.ID;
				if (Plugin.Sicherheitsbehaelter.Value && slot == "SecuredContainer")
				{
					return true;
				}
				if (Plugin.Rucksack.Value && slot == "Backpack")
				{
					return true;
				}
			}
			catch
			{
			}
			return false;
		}

		/// <summary>
		/// Irgendein bereits geladenes GridSortPanel als Vorlage - das des
		/// Lagers. FindObjectsOfTypeAll findet auch ausgeblendete.
		/// </summary>
		private static GridSortPanel Vorlage(ContainedGridsView ansicht)
		{
			GridSortPanel[] alle = Resources.FindObjectsOfTypeAll<GridSortPanel>();
			for (int i = 0; i < alle.Length; i++)
			{
				GridSortPanel p = alle[i];
				if (p == null || p.GetComponent<UnserKnopf>() != null)
				{
					continue;
				}
				if (ansicht != null && p.transform.IsChildOf(ansicht.transform))
				{
					continue;
				}
				return p;
			}
			return null;
		}

		/// <summary>
		/// Nimmt dem Klon alles, womit er Eingaben abfangen koennte.
		///
		/// Das GridSortPanel bringt ein Lade-Overlay mit (_loaderPanel), das
		/// waehrend des Sortierens ueber die Flaeche gelegt wird. Im Klon
		/// wird es nie wieder abgeschaltet - es liegt also unsichtbar im Weg
		/// und schluckt Mausrad und Klicks. Genau das hat das Scrollen in
		/// den Kisten blockiert.
		///
		/// Zusaetzlich wird JEDE Grafik ausser der des Knopfes selbst von der
		/// Eingabe abgemeldet. Danach kann der Klon nur noch dort etwas
		/// abfangen, wo wirklich der Knopf sitzt.
		/// </summary>
		private static void Entschaerfen(GridSortPanel klon)
		{
			try
			{
				foreach (FieldInfo f in typeof(GridSortPanel).GetFields(
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
				{
					if (typeof(GameObject).IsAssignableFrom(f.FieldType))
					{
						GameObject go = f.GetValue(klon) as GameObject;
						if (go != null && go != klon.gameObject)
						{
							go.SetActive(value: false);
							Plugin.Log.LogInfo("Sortierknopf: " + f.Name + " abgeschaltet.");
						}
					}
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("Overlay nicht abschaltbar: " + ex.Message);
			}
			try
			{
				Graphic behalten = null;
				Button knopf = klon.GetComponentInChildren<Button>(includeInactive: true);
				if (knopf != null)
				{
					behalten = knopf.targetGraphic;
				}
				Graphic[] alle = klon.GetComponentsInChildren<Graphic>(includeInactive: true);
				int aus = 0;
				for (int i = 0; i < alle.Length; i++)
				{
					if (alle[i] != null && alle[i] != behalten && alle[i].raycastTarget)
					{
						alle[i].raycastTarget = false;
						aus++;
					}
				}
				if (aus > 0)
				{
					Plugin.Log.LogInfo("Sortierknopf: " + aus + " Grafiken von der Eingabe abgemeldet.");
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("Raycast nicht abschaltbar: " + ex.Message);
			}
		}

		private static void Platzieren(RectTransform rt, RectTransform vorlage)
		{
			if (rt == null)
			{
				return;
			}
			// In eine ECKE haengen und die Groesse der Vorlage uebernehmen.
			// Ohne das behaelt der Klon die Streckung seines urspruenglichen
			// Platzes im Lager - daher die langen Balken quer ueber den
			// Bildschirm.
			float x = (Plugin.Rechts.Value ? 1f : 0f);
			rt.anchorMin = new Vector2(x, 1f);
			rt.anchorMax = new Vector2(x, 1f);
			rt.pivot = new Vector2(x, 1f);
			if (vorlage != null && vorlage.rect.width > 1f && vorlage.rect.height > 1f)
			{
				rt.sizeDelta = new Vector2(vorlage.rect.width, vorlage.rect.height);
			}
			else if (rt.sizeDelta.x < 1f || rt.sizeDelta.y < 1f)
			{
				rt.sizeDelta = new Vector2(28f, 28f);
			}
			rt.offsetMin = new Vector2(rt.offsetMin.x, rt.offsetMin.y);
			rt.anchoredPosition = new Vector2(Plugin.VersatzX.Value, Plugin.VersatzY.Value);
			rt.localScale = Vector3.one * Plugin.Groesse.Value;
			rt.localRotation = Quaternion.identity;

			// Sitzt der Knopf in einem Element mit automatischem Layout,
			// wuerde es ihn sonst sofort wieder woanders hinschieben.
			LayoutElement le = rt.GetComponent<LayoutElement>();
			if (le == null)
			{
				le = rt.gameObject.AddComponent<LayoutElement>();
			}
			le.ignoreLayout = true;
		}

		/// <summary>
		/// Das Element, an das gehaengt wird. Eine Ebene ueber der
		/// Rasteransicht sitzt der Bereich mit der Ueberschrift.
		/// </summary>
		private static Transform Elternteil(Transform ansicht)
		{
			Transform t = ansicht;
			for (int i = 0; i < Plugin.Ebene.Value; i++)
			{
				if (t.parent == null)
				{
					break;
				}
				t = t.parent;
			}
			return t;
		}

		private static InventoryController AusFeld(ContainedGridsView ansicht)
		{
			try
			{
				if (!_gesucht)
				{
					_gesucht = true;
					for (Type t = ansicht.GetType(); t != null && _steuerungFeld == null; t = t.BaseType)
					{
						foreach (FieldInfo f in t.GetFields(BindingFlags.Instance
							| BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
						{
							if (typeof(InventoryController).IsAssignableFrom(f.FieldType))
							{
								_steuerungFeld = f;
								break;
							}
						}
					}
				}
				return _steuerungFeld?.GetValue(ansicht) as InventoryController;
			}
			catch
			{
				return null;
			}
		}
	}

	// ---------------------------------------------------------------
	//  Sortieren im Raid freigeben
	//
	//  Im Raid laesst das Spiel ueberhaupt nicht sortieren - auch
	//  seine eigenen Knoepfe nicht. Der Grund steht in
	//  EFT.InventoryLogic.InventoryController:
	//
	//      public virtual bool IsAllowedToSort(CompoundItem item)
	//      {
	//          return false;
	//      }
	//
	//  Nur BackEndInventoryController - die Steuerung von Menue und
	//  Lager - ueberschreibt das und erlaubt es, wenn der Behaelter
	//  ihr selbst gehoert:
	//
	//      return item.CurrentAddress?.GetOwner() == this;
	//
	//  Im Raid haengt die Spielfigur aber an
	//  SinglePlayerInventoryController. Der erbt ueber
	//  PlayerInventoryController die Fassung, die immer nein sagt -
	//  daher die Meldung "... is not appropriate for sorting".
	//
	//  Hier wird dieselbe Regel angewandt, die BSG im Menue benutzt.
	//  Die Ueberschreibung im BackEndInventoryController bleibt
	//  unberuehrt: Harmony patcht genau eine Methode, und die
	//  ueberschriebene Fassung ist eine andere.
	//
	//  Eingegrenzt auf PlayerInventoryController, damit fremde
	//  Spielerkoerper (ObservedPlayerInventoryController, ebenfalls
	//  direkt von InventoryController abgeleitet) aussen vor
	//  bleiben.
	// ---------------------------------------------------------------
	[HarmonyPatch]
	internal static class ImRaidPatch
	{
		[HarmonyTargetMethod]
		private static MethodBase Ziel()
		{
			return AccessTools.Method(typeof(InventoryController),
				nameof(InventoryController.IsAllowedToSort));
		}

		[HarmonyPostfix]
		private static void Danach(InventoryController __instance, CompoundItem item, ref bool __result)
		{
			try
			{
				// Ein Ja des Spiels nie in ein Nein verwandeln.
				if (__result || Plugin.ImRaid == null || !Plugin.ImRaid.Value)
				{
					return;
				}
				if (item == null || !(__instance is Player.PlayerInventoryController))
				{
					return;
				}

				// Dieselbe Bedingung wie im Menue: der Behaelter muss
				// dieser Steuerung selbst gehoeren. GetOwnerOrNull statt
				// GetOwner - die werfende Fassung wuerde bei einem gerade
				// abgelegten Gegenstand eine Ausnahme ausloesen.
				__result = item.CurrentAddress.GetOwnerOrNull() == __instance;
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("Sortierknopf: Raid-Freigabe - " + ex);
			}
		}
	}
}
