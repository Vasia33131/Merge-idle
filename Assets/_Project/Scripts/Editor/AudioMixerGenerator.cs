using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CatMerge.Audio;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace CatMerge.Editor
{
    /// <summary>
    /// Creates Audio/CatMerge.mixer with Master → Music, SFX groups and exposed volume parameters
    /// (<see cref="AudioService.MusicVolumeParam"/>, <see cref="AudioService.SfxVolumeParam"/>).
    /// Unity has no public API for building mixers, so this goes through internal editor classes via reflection;
    /// if that breaks in a future Unity version the game still works (AudioService falls back to source volume)
    /// and the mixer can be made by hand as described in the log message.
    /// </summary>
    public static class AudioMixerGenerator
    {
        public const string AudioDir = "Assets/_Project/Audio";
        public const string MixerPath = AudioDir + "/CatMerge.mixer";
        public const string MusicGroup = "Music";
        public const string SfxGroup = "SFX";

        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        [MenuItem("Tools/CatMerge/Create Audio Mixer")]
        public static void CreateFromMenu()
        {
            var mixer = LoadOrCreate();
            if (mixer != null) Selection.activeObject = mixer;
        }

        /// <summary>Returns the existing mixer, or creates it. Null if creation failed (logged).</summary>
        public static AudioMixer LoadOrCreate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (existing != null) return existing;

            Directory.CreateDirectory(AudioDir);
            AssetDatabase.Refresh();

            try
            {
                return Create();
            }
            catch (Exception e)
            {
                AssetDatabase.DeleteAsset(MixerPath);
                Debug.LogWarning("[CatMerge] Couldn't generate the audio mixer, sound still works without it. To make it by hand: " +
                                 $"Create > Audio Mixer at {MixerPath}, add groups '{MusicGroup}' and '{SfxGroup}' under Master, " +
                                 $"expose each group's Volume as '{AudioService.MusicVolumeParam}' / '{AudioService.SfxVolumeParam}', " +
                                 $"then rebuild the scene.\n{e}");
                return null;
            }
        }

        public static AudioMixerGroup FindGroup(AudioMixer mixer, string groupName)
        {
            if (mixer == null) return null;
            return mixer.FindMatchingGroups(groupName).FirstOrDefault(g => g.name == groupName);
        }

        private static AudioMixer Create()
        {
            var editorAssembly = typeof(UnityEditor.Editor).Assembly;
            var controllerType = editorAssembly.GetType("UnityEditor.Audio.AudioMixerController", true);
            var groupType = editorAssembly.GetType("UnityEditor.Audio.AudioMixerGroupController", true);
            var pathType = editorAssembly.GetType("UnityEditor.Audio.AudioGroupParameterPath", true);

            var mixer = (AudioMixer)controllerType.GetMethod("CreateMixerControllerAtPath", Any).Invoke(null, new object[] { MixerPath });
            var master = controllerType.GetProperty("masterGroup", Any).GetValue(mixer);

            CreateGroup(mixer, controllerType, groupType, pathType, master, MusicGroup, AudioService.MusicVolumeParam);
            CreateGroup(mixer, controllerType, groupType, pathType, master, SfxGroup, AudioService.SfxVolumeParam);

            EditorUtility.SetDirty(mixer);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(MixerPath);
            var result = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (FindGroup(result, MusicGroup) == null || FindGroup(result, SfxGroup) == null)
                throw new InvalidOperationException("Mixer was created without its groups.");

            Debug.Log($"[CatMerge] Created audio mixer {MixerPath}.", result);
            return result;
        }

        private static void CreateGroup(AudioMixer mixer, Type controllerType, Type groupType, Type pathType, object master,
            string groupName, string exposedName)
        {
            var group = controllerType.GetMethod("CreateNewGroup", Any).Invoke(mixer, new object[] { groupName, false });
            controllerType.GetMethod("AddChildToParent", Any).Invoke(mixer, new[] { group, master });
            controllerType.GetMethod("AddGroupToCurrentView", Any)?.Invoke(mixer, new[] { group });

            var volumeGuid = (GUID)groupType.GetMethod("GetGUIDForVolume", Any).Invoke(group, null);
            var path = Activator.CreateInstance(pathType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { group, (object)volumeGuid }, null);
            controllerType.GetMethod("AddExposedParameter", Any).Invoke(mixer, new[] { path });

            // AddExposedParameter names it "MyExposedParam"; rename the entry that points at this group's volume.
            var exposedProp = controllerType.GetProperty("exposedParameters", Any);
            var exposed = (Array)exposedProp.GetValue(mixer);
            var elementType = exposed.GetType().GetElementType();
            var guidField = elementType.GetField("guid", Any);
            var nameField = elementType.GetField("name", Any);
            for (int i = 0; i < exposed.Length; i++)
            {
                object entry = exposed.GetValue(i);
                if ((GUID)guidField.GetValue(entry) != volumeGuid) continue;
                nameField.SetValue(entry, exposedName);
                exposed.SetValue(entry, i);
            }
            exposedProp.SetValue(mixer, exposed);
        }
    }
}
