import { beforeEach, describe, expect, it } from "vitest";
import { createPinia, setActivePinia } from "pinia";

import { useSettingsStore } from "@/stores/settings";

//oxlint-disable-next-line max-lines-per-function
describe("useSettingsStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("starts with default values", () => {
    const store = useSettingsStore();
    expect(store.accentColor).toBe("amber");
    expect(store.backgroundColor).toBe("parchment");
    expect(store.backgroundImage).toBeNull();
    expect(store.backgroundImageKey).toBeNull();
    expect(store.backgroundImageOpacity).toBe(0.35);
    expect(store.gridIconSize).toBe(48);
    expect(store.listIconSize).toBe(20);
    expect(store.skipDeleteConfirmation).toBe(false);
    expect(store.toastLevel).toBe("all");
    expect(store.isAppearanceSectionOpen).toBe(true);
    expect(store.isBehaviorSectionOpen).toBe(true);
  });

  it("getSettings returns all settings as a flat object", () => {
    const store = useSettingsStore();
    const settings = store.getSettings;
    expect(settings.accentColor).toBe("amber");
    expect(settings.backgroundColor).toBe("parchment");
    expect(settings.gridIconSize).toBe(48);
  });

  describe("accentColor", () => {
    it("setAccentColor updates the value", () => {
      const store = useSettingsStore();
      store.setAccentColor("blue");
      expect(store.accentColor).toBe("blue");
    });
  });

  describe("backgroundColor", () => {
    it("setBackgroundColor updates to a valid preset", () => {
      const store = useSettingsStore();
      store.setBackgroundColor("midnight");
      expect(store.backgroundColor).toBe("midnight");
    });

    it("setBackgroundColor ignores invalid values", () => {
      const store = useSettingsStore();
      store.setBackgroundColor("hot-pink");
      expect(store.backgroundColor).toBe("parchment");
    });
  });

  describe("backgroundImage", () => {
    it("setBackgroundImage updates the value", () => {
      const store = useSettingsStore();
      store.setBackgroundImage("url://img.jpg");
      expect(store.backgroundImage).toBe("url://img.jpg");
    });

    it("setBackgroundImage accepts null", () => {
      const store = useSettingsStore();
      store.setBackgroundImage("url://img.jpg");
      store.setBackgroundImage(null);
      expect(store.backgroundImage).toBeNull();
    });

    it("hasBackgroundImage is false when null", () => {
      const store = useSettingsStore();
      expect(store.hasBackgroundImage).toBe(false);
    });

    it("hasBackgroundImage is true when set", () => {
      const store = useSettingsStore();
      store.setBackgroundImage("url://img.jpg");
      expect(store.hasBackgroundImage).toBe(true);
    });

    it("clearBackgroundImage resets image fields", () => {
      const store = useSettingsStore();
      store.setBackgroundImage("url://img.jpg");
      store.backgroundImageKey = "key-123";
      store.backgroundImageUpdatedAt = "2024-01-01";
      store.clearBackgroundImage();
      expect(store.backgroundImage).toBeNull();
      expect(store.backgroundImageKey).toBeNull();
      expect(store.backgroundImageUpdatedAt).toBeNull();
    });
  });

  describe("backgroundImageOpacity", () => {
    it("setBackgroundImageOpacity clamps to min 0.1", () => {
      const store = useSettingsStore();
      store.setBackgroundImageOpacity(0);
      expect(store.backgroundImageOpacity).toBe(0.1);
    });

    it("setBackgroundImageOpacity clamps to max 0.65", () => {
      const store = useSettingsStore();
      store.setBackgroundImageOpacity(1);
      expect(store.backgroundImageOpacity).toBe(0.65);
    });

    it("setBackgroundImageOpacity accepts values in range", () => {
      const store = useSettingsStore();
      store.setBackgroundImageOpacity(0.4);
      expect(store.backgroundImageOpacity).toBe(0.4);
    });
  });

  describe("gridIconSize", () => {
    it("setGridIconSize clamps to min 12", () => {
      const store = useSettingsStore();
      store.setGridIconSize(0);
      expect(store.gridIconSize).toBe(12);
    });

    it("setGridIconSize clamps to max 64", () => {
      const store = useSettingsStore();
      store.setGridIconSize(100);
      expect(store.gridIconSize).toBe(64);
    });

    it("setGridIconSize accepts values in range", () => {
      const store = useSettingsStore();
      store.setGridIconSize(32);
      expect(store.gridIconSize).toBe(32);
    });
  });

  describe("listIconSize", () => {
    it("setListIconSize clamps to min 12", () => {
      const store = useSettingsStore();
      store.setListIconSize(0);
      expect(store.listIconSize).toBe(12);
    });

    it("setListIconSize clamps to max 64", () => {
      const store = useSettingsStore();
      store.setListIconSize(100);
      expect(store.listIconSize).toBe(64);
    });
  });

  describe("skipDeleteConfirmation", () => {
    it("setSkipDeleteConfirmation updates the value", () => {
      const store = useSettingsStore();
      store.setSkipDeleteConfirmation(true);
      expect(store.skipDeleteConfirmation).toBe(true);
      store.setSkipDeleteConfirmation(false);
      expect(store.skipDeleteConfirmation).toBe(false);
    });
  });

  describe("toastLevel", () => {
    it("setToastLevel updates the value", () => {
      const store = useSettingsStore();
      store.setToastLevel("silent");
      expect(store.toastLevel).toBe("silent");
    });
  });

  describe("UI section toggles", () => {
    it("setAppearanceSectionOpen updates the value", () => {
      const store = useSettingsStore();
      store.setAppearanceSectionOpen(false);
      expect(store.isAppearanceSectionOpen).toBe(false);
    });

    it("setBehaviorSectionOpen updates the value", () => {
      const store = useSettingsStore();
      store.setBehaviorSectionOpen(false);
      expect(store.isBehaviorSectionOpen).toBe(false);
    });
  });

  describe("getCurrentBackgroundPreset", () => {
    it("returns the matching preset", () => {
      const store = useSettingsStore();
      const preset = store.getCurrentBackgroundPreset;
      expect(preset.name).toBe("parchment");
    });

    it("returns the first preset for unknown background", () => {
      const store = useSettingsStore();
      store.backgroundColor = "nonexistent";
      const preset = store.getCurrentBackgroundPreset;
      expect(preset.name).toBe("system");
    });
  });

  describe("syncFromServer", () => {
    it("syncAppearanceFromServer updates appearance fields", () => {
      const store = useSettingsStore();
      store.syncAppearanceFromServer({
        accentColor: "green",
        backgroundColor: "ink",
        backgroundImageKey: "img-key",
        backgroundImageOpacity: 0.5,
        backgroundImageUpdatedAt: "2024-06-01",
        gridIconSize: 32,
        listIconSize: 16,
      });
      expect(store.accentColor).toBe("green");
      expect(store.backgroundColor).toBe("ink");
      expect(store.backgroundImageKey).toBe("img-key");
      expect(store.backgroundImageOpacity).toBe(0.5);
      expect(store.gridIconSize).toBe(32);
      expect(store.listIconSize).toBe(16);
    });

    it("syncBehaviorFromServer updates behavior fields", () => {
      const store = useSettingsStore();
      store.syncBehaviorFromServer({
        skipDeleteConfirmation: true,
        toastLevel: "errors-only",
      });
      expect(store.skipDeleteConfirmation).toBe(true);
      expect(store.toastLevel).toBe("errors-only");
    });

    it("syncFromServer updates both appearance and behavior", () => {
      const store = useSettingsStore();
      store.syncFromServer(
        { accentColor: "red", backgroundColor: "cool", backgroundImageKey: null, backgroundImageOpacity: 0.3, backgroundImageUpdatedAt: null, gridIconSize: 24, listIconSize: 14 },
        { skipDeleteConfirmation: true, toastLevel: "silent" },
      );
      expect(store.accentColor).toBe("red");
      expect(store.skipDeleteConfirmation).toBe(true);
    });
  });

  describe("updateSettings", () => {
    it("updates only the provided fields", () => {
      const store = useSettingsStore();
      store.updateSettings({ accentColor: "purple", toastLevel: "errors-only" });
      expect(store.accentColor).toBe("purple");
      expect(store.toastLevel).toBe("errors-only");
      expect(store.gridIconSize).toBe(48);
    });

    it("ignores undefined fields", () => {
      const store = useSettingsStore();
      store.updateSettings({});
      expect(store.accentColor).toBe("amber");
    });
  });

  describe("resetSettings", () => {
    it("resets all settings to defaults", () => {
      const store = useSettingsStore();
      store.setAccentColor("blue");
      store.setBackgroundColor("midnight");
      store.setGridIconSize(64);
      store.setSkipDeleteConfirmation(true);
      store.resetSettings();
      expect(store.accentColor).toBe("amber");
      expect(store.backgroundColor).toBe("parchment");
      expect(store.gridIconSize).toBe(48);
      expect(store.skipDeleteConfirmation).toBe(false);
    });
  });

  describe("resetAppearanceSettings", () => {
    it("resets only appearance fields", () => {
      const store = useSettingsStore();
      store.setAccentColor("blue");
      store.setGridIconSize(64);
      store.setSkipDeleteConfirmation(true);
      store.resetAppearanceSettings();
      expect(store.accentColor).toBe("amber");
      expect(store.gridIconSize).toBe(48);
      expect(store.skipDeleteConfirmation).toBe(true);
    });
  });

  describe("resetBehaviorSettings", () => {
    it("resets only behavior fields", () => {
      const store = useSettingsStore();
      store.setSkipDeleteConfirmation(true);
      store.setToastLevel("silent");
      store.setAccentColor("blue");
      store.resetBehaviorSettings();
      expect(store.skipDeleteConfirmation).toBe(false);
      expect(store.toastLevel).toBe("all");
      expect(store.accentColor).toBe("blue");
    });
  });

  describe("resetUIState", () => {
    it("resets UI section toggles", () => {
      const store = useSettingsStore();
      store.setAppearanceSectionOpen(false);
      store.setBehaviorSectionOpen(false);
      store.resetUIState();
      expect(store.isAppearanceSectionOpen).toBe(true);
      expect(store.isBehaviorSectionOpen).toBe(true);
    });
  });
});
