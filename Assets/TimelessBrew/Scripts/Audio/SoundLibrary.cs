using System.Collections.Generic;
using UnityEngine;

namespace TimelessBrew.Audio
{
    /// <summary>
    /// Реестр клипов: <see cref="Sfx"/>/<see cref="Music"/> → файл в Resources/Audio/* по слагу.
    /// Грузит лениво и кэширует. Если файла нет (событие пока не озвучено) — возвращает null,
    /// и звук просто не играет (без ошибок). Слаги совпадают с именами .mp3 в Resources.
    /// </summary>
    public static class SoundLibrary
    {
        private static readonly Dictionary<string, AudioClip> _cache = new();

        public static AudioClip Get(Sfx key)
        {
            string slug = Slug(key);
            return slug == null ? null : Load("Audio/SFX/" + slug);
        }

        public static AudioClip Get(Music key)
        {
            string slug = Slug(key);
            return slug == null ? null : Load("Audio/Music/" + slug);
        }

        private static AudioClip Load(string path)
        {
            if (_cache.TryGetValue(path, out var clip)) return clip;
            clip = Resources.Load<AudioClip>(path);   // без расширения
            _cache[path] = clip;                       // кэшируем и null — не дёргать диск повторно
            return clip;
        }

        private static string Slug(Sfx k) => k switch
        {
            Sfx.TakePlaceMilk => "take_place_milk",
            Sfx.TakePlacePan => "take_place_pan",
            Sfx.TakePlaceBeanJar => "take_place_bean_jar",
            Sfx.PlaceKettle => "place_kettle",
            Sfx.PlaceCupSaucer => "place_cup_saucer",
            Sfx.PlaceTray => "place_tray",
            Sfx.PourBeans => "pour_beans",
            Sfx.PourWater => "pour_water",
            Sfx.PourCoffee => "pour_coffee",
            Sfx.PourMilk => "pour_milk",
            Sfx.RoastSizzle => "roast_sizzle",
            Sfx.Overheat => "overheat",
            Sfx.Grind => "grind",
            Sfx.GrindEmpty => "grind_empty",
            Sfx.KettleBoil => "kettle_boil",
            Sfx.CezveBrew => "cezve_brew",
            Sfx.SpoonStir => "spoon_stir",
            Sfx.SinkFill => "sink_fill",
            Sfx.Sift => "sift",
            Sfx.BeansInColander => "beans_in_colander",
            Sfx.Spice => "spice",
            Sfx.SugarCube => "sugar_cube",
            Sfx.SugarOpen => "sugar_open",
            Sfx.Syrup => "syrup",
            Sfx.Trash => "trash",
            Sfx.BellServe => "bell_serve",
            Sfx.GuestCall => "guest_call",
            Sfx.RoastModeSwitch => "roast_mode_switch",
            _ => null
        };

        private static string Slug(Music k) => k switch
        {
            Music.Menu => "menu",
            Music.Gameplay => "gameplay",
            Music.EveningAmbience => "evening_ambience",
            Music.Endshift => "endshift",
            Music.NatureLoop => "nature_loop",
            _ => null
        };
    }
}
