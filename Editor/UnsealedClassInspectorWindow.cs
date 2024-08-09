using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnsealedClassFinder.Editor
{
    public sealed class UnsealedClassInspectorWindow : EditorWindow
    {
        private const string PrefKeyHiddenAssemblies = "unsealedclass-hidden";
        private static List<string> _hiddenAssemblies;
        public static List<string> HiddenAssemblies
        {
            get
            {
                if (_hiddenAssemblies == null)
                {
                    var rawPref = EditorPrefs.GetString(PrefKeyHiddenAssemblies, string.Empty);
                    _hiddenAssemblies = !string.IsNullOrEmpty(rawPref) ? rawPref.Split(";").ToList() : new List<string>();
                    
                    //Doesn't really matter if assemblies added multiple times, but we'll flush on load cuz why not
                    _hiddenAssemblies = _hiddenAssemblies.Distinct().ToList();
                }

                return _hiddenAssemblies;
            }
        }

        private static readonly List<string> DefaultHideAssemblyPrefixes = new()
        {
            "Unity",
            "System.",
            "Mono.",
            "log4",
            "mscorlib",
            "netstandard",
            "nunit",
            "bee.",
            "jetbrains.",
            "reportgeneratormerged",
            "unsealedclassfinder"
        };
        
        private Vector2 _scrollPosSidebar= Vector2.zero;
        private Vector2 _scrollPosInspector = Vector2.zero;

        private SortedDictionary<Assembly, List<Type>> _assemblies = new();
        private Assembly _selectedAssembly;

        private GUIStyle _styleSidebarButton;

        [MenuItem("Window/Analysis/Unsealed Class Inspector")]
        public static void Initialize()
        {
            var window = GetWindow<UnsealedClassInspectorWindow>("Unsealed Classes");
            window.Show();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void CreateStyles()
        {
            // ReSharper disable once ConvertIfStatementToNullCoalescingAssignment
            if (_styleSidebarButton == null)
            {
                _styleSidebarButton = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(5,5,10,10)
                };
            }
        }

        public void OnGUI()
        {
            CreateStyles();
            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            {
                DrawSidebar();
                DrawInspector();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                if (_selectedAssembly != null)
                {
                    if (HiddenAssemblies.Contains(_selectedAssembly.GetName().Name))
                    {
                        if (GUILayout.Button("Show Selected", EditorStyles.toolbarButton))
                        {
                            HiddenAssemblies.Remove(_selectedAssembly.GetName().Name);
                            EditorPrefs.SetString(PrefKeyHiddenAssemblies, string.Join(";", _hiddenAssemblies));
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Hide Selected", EditorStyles.toolbarButton))
                        {
                            HiddenAssemblies.Add(_selectedAssembly.GetName().Name);
                            EditorPrefs.SetString(PrefKeyHiddenAssemblies, string.Join(";", _hiddenAssemblies));
                            _selectedAssembly = null;
                        }
                    }
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Hide Defaults", EditorStyles.toolbarButton))
                {
                    foreach (var assembly in _assemblies)
                    {
                        var assemblyName = assembly.Key.GetName().Name;
                        if (HiddenAssemblies.Contains(assemblyName))
                        {
                            continue;
                        }
                            
                        if (DefaultHideAssemblyPrefixes.Any(prefix => assemblyName.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase)))
                        {
                            HiddenAssemblies.Add(assemblyName);
                        }

                        if (assemblyName.Equals("system", StringComparison.InvariantCultureIgnoreCase))
                        {
                            HiddenAssemblies.Add(assemblyName);
                        }
                    }
                        
                    EditorPrefs.SetString(PrefKeyHiddenAssemblies, string.Join(";", _hiddenAssemblies));
                }
                if (GUILayout.Button("Show All", EditorStyles.toolbarButton))
                {
                    HiddenAssemblies.Clear();
                }
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
                {
                    Refresh();
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawSidebar()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Width(273));
            {
                _scrollPosSidebar = EditorGUILayout.BeginScrollView(_scrollPosSidebar);
                {
                    foreach (var assembly in _assemblies)
                    {
                        GUI.color = _selectedAssembly == assembly.Key ? Color.cyan : HiddenAssemblies.Contains(assembly.Key.GetName().Name) ? Color.gray : Color.white;
                        if (GUILayout.Button($"{assembly.Key.GetName().Name} ({assembly.Value.Count})", _styleSidebarButton, GUILayout.Width(245)))
                        {
                            _selectedAssembly = assembly.Key;
                        }
                    }
                    
                    GUI.color = Color.white;
                }
                EditorGUILayout.EndScrollView();
                
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndVertical();
        }
        
        private void DrawInspector()
        {
            EditorGUILayout.BeginVertical();
            {
                _scrollPosInspector = EditorGUILayout.BeginScrollView(_scrollPosInspector, GUILayout.ExpandWidth(true));
                {
                    if (_selectedAssembly != null)
                    {
                        foreach (var type in _assemblies[_selectedAssembly])
                        {
                            EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));
                            {
                                GUILayout.Label(type.Name, EditorStyles.boldLabel);
                                GUILayout.Label(type.FullName, EditorStyles.miniLabel);
                            }
                            EditorGUILayout.EndVertical();
                            
                            //Disabled since this isn't very reliable and clunky
                            // ===============================================
                            /*var currentEvent = Event.current;
                            var lastRect = GUILayoutUtility.GetLastRect();
                            if (currentEvent.type == EventType.MouseDown 
                                && currentEvent.button == 0 
                                && lastRect.Contains(currentEvent.mousePosition))
                            {
                                currentEvent.Use();
                                if (IsTypeInProjectOrUpm(type))
                                {
                                    OpenScriptInEditor(type);
                                }
                            }*/
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndVertical();
        }

        private void Refresh()
        {
            _scrollPosInspector = Vector2.zero;
            _selectedAssembly = null;
            _assemblies = new SortedDictionary<Assembly, List<Type>>(new AssemblyCustomComparer());
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                _assemblies.Add(assembly, new List<Type>());
                foreach (var type in assembly.GetTypes())
                {
                    if (!type.IsAbstract && type.IsClass && !type.IsSealed)
                    {
                        if (type.Name.StartsWith("UnitySourceGeneratedAssemblyMonoScript"))
                        {
                            continue;
                        }
                    
                        _assemblies[assembly].Add(type);
                    }
                }
            }
        }

        private static void OpenScriptInEditor(Type type)
        {
            var typeFilePath = GetTypeFilePath(type);
            if (string.IsNullOrEmpty(typeFilePath))
            {
                Debug.LogError($"Could not find script file for type: {type.FullName}");
                return;
            }

            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<MonoScript>(typeFilePath));
        }

        private static bool IsTypeInProjectOrUpm(Type type)
        {
            var typeFilePath = GetTypeFilePath(type);
            if (!string.IsNullOrEmpty(typeFilePath) && File.Exists(typeFilePath))
            {
                return true;
            }

            var packagePaths = AssetDatabase.GetAllAssetPaths();
            foreach (var packagePath in packagePaths)
            {
                if (packagePath.Contains("Packages/") && File.Exists(packagePath))
                {
                    //TODO Make scanning for types more efficient and reliable
                    var lines = File.ReadAllLines(packagePath);
                    foreach (var line in lines)
                    {
                        if (line.Contains(type.Name))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static string GetTypeFilePath(Type type)
        {
            if (type == null)
            {
                return string.Empty;
            }
            
            // Convert type's full name to file path
            var typeName = type.FullName.Replace('.', '/') + ".cs";
            var guids = AssetDatabase.FindAssets(typeName);
            if (guids.Length > 0)
            {
                var filePath = AssetDatabase.GUIDToAssetPath(guids[0]);
                return Path.Combine(Application.dataPath, filePath.Substring("Assets/".Length));
            }
            
            return string.Empty;
        }
        
        private sealed class AssemblyCustomComparer : IComparer<Assembly>
        {
            public int Compare(Assembly x, Assembly y)
            {
                if (x == null)
                {
                    throw new ArgumentNullException(nameof(x));
                }

                if (y == null)
                {
                    throw new ArgumentNullException(nameof(y));
                }

                var xIsBottom = HiddenAssemblies.Contains(x.GetName().Name);
                var yIsBottom = HiddenAssemblies.Contains(y.GetName().Name);

                return xIsBottom switch
                {
                    true when !yIsBottom => 1 // x is considered greater (placed at the bottom)
                    ,
                    false when yIsBottom => -1 // y is considered greater (placed at the bottom)
                    ,
                    _ => string.Compare(x.GetName().Name, y.GetName().Name, StringComparison.Ordinal)
                };
            }
        }
    }
}