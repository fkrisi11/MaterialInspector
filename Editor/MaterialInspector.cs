#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class MaterialInspector : EditorWindow
{
    // Tab management
    private List<TabData> tabs = new List<TabData>();
    private int activeTabIndex = 0;
    private Vector2 tabScrollPosition;
    private Material copiedMaterial;
    private int draggedTabIndex = -1;
    private bool isDraggingTab = false;
    private Vector2 dragStartPosition;
    private List<Rect> tabRects = new List<Rect>();

    // Current tab data shortcuts
    private TabData ActiveTab => tabs.Count > 0 && activeTabIndex < tabs.Count ? tabs[activeTabIndex] : null;

    [Serializable]
    private class TabData
    {
        public string name = "<no mat>";
        public Material material;
        public string search = "";
        public Vector2 scroll;

        // Filter options
        public bool showFilterOptions = false;
        public int minResolution = 0;
        public int maxResolution = 8192;
        public bool filterByCrunch = false;
        public bool showOnlyCrunched = true;
        public bool filterByColorSpace = false;
        public ColorSpaceFilter colorSpaceFilter = ColorSpaceFilter.All;
        public bool filterByFormat = false;
        public string formatFilter = "";
    }

    private enum ColorSpaceFilter
    {
        All,
        sRGB,
        Linear,
        NormalMaps
    }

    [MenuItem("TohruTheDragon/Material Inspector")]
    public static void ShowWindow()
    {
        var window = GetWindow<MaterialInspector>();
        window.titleContent = new GUIContent("Material Inspector");

        // Initialize with one empty tab if no tabs exist
        if (window.tabs.Count == 0)
        {
            window.AddNewTab();
        }

        window.Show();
    }


    private void OnEnable()
    {
        // Subscribe to asset change events
        EditorApplication.projectChanged += OnProjectChanged;

        // Initialize with one tab if none exist
        if (tabs.Count == 0)
        {
            AddNewTab();
        }

        // Check for material changes on enable
        ValidateAllMaterials();
    }

    private void OnDisable()
    {
        // Unsubscribe from asset change events
        EditorApplication.projectChanged -= OnProjectChanged;
    }

    private void OnProjectChanged()
    {
        // Called when assets are imported, deleted, or moved
        ValidateAllMaterials();
        Repaint();
    }

    private void ValidateAllMaterials()
    {
        bool anyChanges = false;

        for (int i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];

            if (tab.material == null)
            {
                // Material is null, update tab name if needed
                if (tab.name != "<no mat>")
                {
                    tab.name = "<no mat>";
                    anyChanges = true;
                }
            }
            else
            {
                // Check if material still exists
                string assetPath = AssetDatabase.GetAssetPath(tab.material);

                if (string.IsNullOrEmpty(assetPath) || !System.IO.File.Exists(assetPath))
                {
                    // Material was deleted
                    tab.material = null;
                    tab.name = "<no mat>";
                    anyChanges = true;
                }
                else
                {
                    // Material exists, check if name changed
                    string currentMaterialName = tab.material.name;
                    if (tab.name != currentMaterialName)
                    {
                        tab.name = currentMaterialName;
                        anyChanges = true;
                    }
                }
            }
        }

        if (anyChanges)
        {
            Repaint();
        }
    }

    private void ValidateCurrentTabMaterial()
    {
        if (ActiveTab == null) return;

        if (ActiveTab.material == null)
        {
            ActiveTab.name = "<no mat>";
        }
        else
        {
            // Check if material still exists
            string assetPath = AssetDatabase.GetAssetPath(ActiveTab.material);

            if (string.IsNullOrEmpty(assetPath) || !System.IO.File.Exists(assetPath))
            {
                // Material was deleted
                ActiveTab.material = null;
                ActiveTab.name = "<no mat>";
            }
            else
            {
                // Material exists, check if name changed
                string currentMaterialName = ActiveTab.material.name;
                if (ActiveTab.name != currentMaterialName)
                {
                    ActiveTab.name = currentMaterialName;
                }
            }
        }
    }

    private void OnGUI()
    {
        ValidateCurrentTabMaterial();


        DrawTabBar();

        if (ActiveTab == null) return;

        EditorGUILayout.Space();
        DrawMaterialField();

        if (ActiveTab.material == null || ActiveTab.material.shader == null)
        {
            EditorGUILayout.HelpBox("Please assign a material.", MessageType.Info);
            return;
        }

        DrawSearchAndFilters();
        DrawTextureList();
    }

    private void DrawTabBar()
    {
        EditorGUILayout.BeginHorizontal();

        // Clear and rebuild tab rects
        tabRects.Clear();

        // Tab scroll area with proper scrollbar
        EditorGUILayout.BeginVertical();
        tabScrollPosition = EditorGUILayout.BeginScrollView(tabScrollPosition, false, true,
            GUI.skin.horizontalScrollbar, GUIStyle.none, GUIStyle.none, GUILayout.Height(30));

        EditorGUILayout.BeginHorizontal(GUILayout.Height(25));

        for (int i = 0; i < tabs.Count; i++)
        {
            DrawTab(i);
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // Add new tab button with drag and drop support
        DrawAddTabButton();

        EditorGUILayout.EndHorizontal();

        // Handle tab rearranging after all tabs are drawn
        HandleTabDragAndDrop();

        // Tab separator line
        EditorGUILayout.Space(2);
        Rect lineRect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(lineRect, new Color(0.5f, 0.5f, 0.5f, 1));
    }

    private void DrawTab(int index)
    {
        var tab = tabs[index];

        // Enhanced tab button style with clear active state
        GUIStyle tabStyle = new GUIStyle(EditorStyles.toolbarButton);

        bool isActive = index == activeTabIndex;
        bool isDragged = isDraggingTab && draggedTabIndex == index;

        // Style the active tab prominently
        if (isActive)
        {
            tabStyle.normal.background = EditorStyles.toolbarButton.active.background;
            tabStyle.normal.textColor = Color.white;
            tabStyle.fontStyle = FontStyle.Bold;
            tabStyle.border = new RectOffset(2, 2, 2, 2);
        }
        else
        {
            tabStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            tabStyle.fontStyle = FontStyle.Normal;
        }

        if (isDragged)
        {
            tabStyle.normal.background = EditorStyles.toolbarButton.hover.background;
            tabStyle.normal.textColor = Color.yellow;
        }

        // Calculate tab width to fit the full text
        GUIContent tabContent = new GUIContent(tab.name);
        Vector2 contentSize = tabStyle.CalcSize(tabContent);

        // Always make tabs wide enough for their content with some padding
        float tabWidth = Mathf.Max(80, contentSize.x + 20); // Minimum 80px, with 20px padding

        // Get the tab rect and store it for drag detection
        Rect tabRect = GUILayoutUtility.GetRect(tabWidth, 25, tabStyle);
        tabRects.Add(tabRect);

        // Handle events
        Event currentEvent = Event.current;
        bool mouseInTab = tabRect.Contains(currentEvent.mousePosition);

        // Handle drag and drop for materials onto tabs
        HandleMaterialDropOnTab(tabRect, index);

        // Handle tab click and drag events
        if (mouseInTab)
        {
            if (currentEvent.type == EventType.MouseDown)
            {
                if (currentEvent.button == 0) // Left click
                {
                    activeTabIndex = index;
                    draggedTabIndex = index;
                    isDraggingTab = false;
                    dragStartPosition = currentEvent.mousePosition;
                    currentEvent.Use();
                }
                else if (currentEvent.button == 1) // Right click
                {
                    ShowTabContextMenu(index);
                    currentEvent.Use();
                }
            }
            else if (currentEvent.type == EventType.MouseDrag && draggedTabIndex == index && currentEvent.button == 0)
            {
                if (!isDraggingTab && Vector2.Distance(currentEvent.mousePosition, dragStartPosition) > 10)
                {
                    isDraggingTab = true;
                }
                if (isDraggingTab)
                {
                    Repaint();
                    currentEvent.Use();
                }
            }
        }

        // Handle mouse up globally for drag end
        if (currentEvent.type == EventType.MouseUp && isDraggingTab && draggedTabIndex == index)
        {
            isDraggingTab = false;
            draggedTabIndex = -1;
            currentEvent.Use();
        }

        // Draw the tab button with full text
        GUI.Button(tabRect, tabContent, tabStyle);

        // Visual indicators for active tab
        if (isActive)
        {
            Rect underlineRect = new Rect(tabRect.x, tabRect.yMax - 3, tabRect.width, 3);
            EditorGUI.DrawRect(underlineRect, new Color(0.3f, 0.7f, 1f, 1f));

            EditorGUI.DrawRect(new Rect(tabRect.x, tabRect.y, 2, tabRect.height), new Color(0.3f, 0.7f, 1f, 0.8f));
            EditorGUI.DrawRect(new Rect(tabRect.xMax - 2, tabRect.y, 2, tabRect.height), new Color(0.3f, 0.7f, 1f, 0.8f));
        }

        // Drag indicators
        if (isDragged)
        {
            EditorGUI.DrawRect(new Rect(tabRect.x, tabRect.y - 2, tabRect.width, 2), Color.cyan);
            EditorGUI.DrawRect(new Rect(tabRect.x, tabRect.yMax, tabRect.width, 2), Color.cyan);
        }
    }

    private void DrawAddTabButton()
    {
        Rect addButtonRect = GUILayoutUtility.GetRect(25, 25, EditorStyles.toolbarButton);

        // Handle drag and drop for materials onto the + button
        HandleMaterialDropOnAddButton(addButtonRect);

        if (GUI.Button(addButtonRect, "+", EditorStyles.toolbarButton))
        {
            AddNewTab();
        }
    }

    private void HandleMaterialDropOnTab(Rect tabRect, int tabIndex)
    {
        Event currentEvent = Event.current;

        switch (currentEvent.type)
        {
            case EventType.DragUpdated:
                if (tabRect.Contains(currentEvent.mousePosition))
                {
                    if (DragAndDrop.objectReferences.Length > 0 &&
                        DragAndDrop.objectReferences[0] is Material)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        currentEvent.Use();
                    }
                }
                break;

            case EventType.DragPerform:
                if (tabRect.Contains(currentEvent.mousePosition))
                {
                    if (DragAndDrop.objectReferences.Length > 0 &&
                        DragAndDrop.objectReferences[0] is Material material)
                    {
                        DragAndDrop.AcceptDrag();
                        tabs[tabIndex].material = material;
                        tabs[tabIndex].name = material.name;
                        activeTabIndex = tabIndex;
                        currentEvent.Use();
                    }
                }
                break;
        }
    }

    private void HandleMaterialDropOnAddButton(Rect addButtonRect)
    {
        Event currentEvent = Event.current;

        switch (currentEvent.type)
        {
            case EventType.DragUpdated:
                if (addButtonRect.Contains(currentEvent.mousePosition))
                {
                    if (HasMaterialsInDrag())
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        currentEvent.Use();
                    }
                }
                break;

            case EventType.DragPerform:
                if (addButtonRect.Contains(currentEvent.mousePosition))
                {
                    var materials = GetMaterialsFromDrag();
                    if (materials.Count > 0)
                    {
                        DragAndDrop.AcceptDrag();

                        // Create a tab for each material
                        int firstNewTabIndex = tabs.Count;
                        foreach (var material in materials)
                        {
                            var newTab = new TabData
                            {
                                material = material,
                                name = material.name
                            };
                            tabs.Add(newTab);
                        }

                        // Switch to the first new tab
                        activeTabIndex = firstNewTabIndex;
                        currentEvent.Use();
                    }
                }
                break;
        }
    }

    private bool HasMaterialsInDrag()
    {
        return DragAndDrop.objectReferences.Any(obj => obj is Material);
    }

    private List<Material> GetMaterialsFromDrag()
    {
        return DragAndDrop.objectReferences
            .Where(obj => obj is Material)
            .Cast<Material>()
            .ToList();
    }

    private void HandleTabDragAndDrop()
    {
        if (!isDraggingTab || draggedTabIndex == -1) return;

        Event currentEvent = Event.current;

        if (currentEvent.type == EventType.MouseDrag)
        {
            Vector2 mousePos = currentEvent.mousePosition;
            int insertIndex = -1;

            for (int i = 0; i < tabRects.Count; i++)
            {
                Rect tabRect = tabRects[i];
                float tabCenterX = tabRect.x + tabRect.width / 2;

                if (mousePos.x < tabCenterX)
                {
                    insertIndex = i;
                    break;
                }
            }

            // If no insert position found, insert at end
            if (insertIndex == -1)
            {
                insertIndex = tabs.Count;
            }

            // Don't move if we're trying to insert in the same position
            if (insertIndex == draggedTabIndex || insertIndex == draggedTabIndex + 1)
            {
                return;
            }

            // Perform the move
            var draggedTab = tabs[draggedTabIndex];
            tabs.RemoveAt(draggedTabIndex);

            // Adjust insert index if we removed something before it
            if (insertIndex > draggedTabIndex)
            {
                insertIndex--;
            }

            // Clamp and insert
            insertIndex = Mathf.Clamp(insertIndex, 0, tabs.Count);
            tabs.Insert(insertIndex, draggedTab);

            // Update active tab index
            if (activeTabIndex == draggedTabIndex)
            {
                activeTabIndex = insertIndex;
            }
            else if (activeTabIndex > draggedTabIndex && activeTabIndex <= insertIndex)
            {
                activeTabIndex--;
            }
            else if (activeTabIndex < draggedTabIndex && activeTabIndex >= insertIndex)
            {
                activeTabIndex++;
            }

            draggedTabIndex = insertIndex;
            Repaint();
        }
    }

    private void ShowTabContextMenu(int tabIndex)
    {
        GenericMenu menu = new GenericMenu();

        // Copy Material
        if (tabs[tabIndex].material != null)
        {
            menu.AddItem(new GUIContent("Copy Material"), false, () => {
                copiedMaterial = tabs[tabIndex].material;
            });
        }
        else
        {
            menu.AddDisabledItem(new GUIContent("Copy Material"));
        }

        // Paste Material
        if (copiedMaterial != null)
        {
            menu.AddItem(new GUIContent($"Paste Material ({copiedMaterial.name})"), false, () => {
                tabs[tabIndex].material = copiedMaterial;
                tabs[tabIndex].name = copiedMaterial.name;
            });
        }
        else
        {
            menu.AddDisabledItem(new GUIContent("Paste Material"));
        }

        menu.AddSeparator("");

        // Reset Material
        if (tabs[tabIndex].material != null)
        {
            menu.AddItem(new GUIContent("Reset Material"), false, () => {
                tabs[tabIndex].material = null;
                tabs[tabIndex].name = "<no mat>";
            });
        }
        else
        {
            menu.AddDisabledItem(new GUIContent("Reset Material"));
        }

        menu.AddSeparator("");

        // Duplicate Tab
        menu.AddItem(new GUIContent("Duplicate Tab"), false, () => {
            DuplicateTab(tabIndex);
        });

        menu.AddSeparator("");

        // Close Tab
        if (tabs.Count > 1)
        {
            menu.AddItem(new GUIContent("Close Tab"), false, () => {
                CloseTab(tabIndex);
            });
        }
        else
        {
            menu.AddDisabledItem(new GUIContent("Close Tab"));
        }

        // Close Other Tabs
        if (tabs.Count > 1)
        {
            menu.AddItem(new GUIContent("Close Other Tabs"), false, () => {
                CloseOtherTabs(tabIndex);
            });
        }
        else
        {
            menu.AddDisabledItem(new GUIContent("Close Other Tabs"));
        }

        // Close Tabs to the Right
        if (tabIndex < tabs.Count - 1)
        {
            menu.AddItem(new GUIContent("Close Tabs to the Right"), false, () => {
                CloseTabsToTheRight(tabIndex);
            });
        }
        else
        {
            menu.AddDisabledItem(new GUIContent("Close Tabs to the Right"));
        }

        menu.ShowAsContext();
    }

    private void DuplicateTab(int index)
    {
        var originalTab = tabs[index];
        var newTab = new TabData
        {
            material = originalTab.material,
            name = originalTab.material != null ? $"{originalTab.material.name}" : "<no mat>",
            search = originalTab.search,
            showFilterOptions = originalTab.showFilterOptions,
            minResolution = originalTab.minResolution,
            maxResolution = originalTab.maxResolution,
            filterByCrunch = originalTab.filterByCrunch,
            showOnlyCrunched = originalTab.showOnlyCrunched,
            filterByColorSpace = originalTab.filterByColorSpace,
            colorSpaceFilter = originalTab.colorSpaceFilter,
            filterByFormat = originalTab.filterByFormat,
            formatFilter = originalTab.formatFilter
        };

        tabs.Insert(index + 1, newTab);
        activeTabIndex = index + 1;
    }

    private void CloseTabsToTheRight(int index)
    {
        if (index >= tabs.Count - 1) return;

        int tabsToRemove = tabs.Count - index - 1;
        tabs.RemoveRange(index + 1, tabsToRemove);

        // Adjust active tab if necessary
        if (activeTabIndex > index)
        {
            activeTabIndex = index;
        }
    }

    private void AddNewTab()
    {
        var newTab = new TabData();
        tabs.Add(newTab);
        activeTabIndex = tabs.Count - 1;
    }

    private void CloseTab(int index)
    {
        if (tabs.Count <= 1) return;

        tabs.RemoveAt(index);

        // Adjust active tab index
        if (activeTabIndex >= tabs.Count)
        {
            activeTabIndex = tabs.Count - 1;
        }
        else if (activeTabIndex > index)
        {
            activeTabIndex--;
        }
    }

    private void CloseOtherTabs(int keepIndex)
    {
        var tabToKeep = tabs[keepIndex];
        tabs.Clear();
        tabs.Add(tabToKeep);
        activeTabIndex = 0;
    }

    private void DrawMaterialField()
    {
        if (ActiveTab == null) return;

        string matPath = ActiveTab.material ? AssetDatabase.GetAssetPath(ActiveTab.material) : "";
        GUIContent materialLabel = new GUIContent("Material", string.IsNullOrEmpty(matPath) ? "Drop a material here" : $"Path: {matPath}");

        Material previousMaterial = ActiveTab.material;
        ActiveTab.material = (Material)EditorGUILayout.ObjectField(materialLabel, ActiveTab.material, typeof(Material), false);

        // Update tab name when material changes
        if (ActiveTab.material != previousMaterial)
        {
            ActiveTab.name = ActiveTab.material != null ? ActiveTab.material.name : "<no mat>";
        }
    }

    private void DrawSearchAndFilters()
    {
        if (ActiveTab == null) return;

        EditorGUILayout.Space();

        // Search field
        GUIContent searchLabel = new GUIContent("Search",
                                                "Filter textures using:\n" +
                                                "- Comma `,` for AND (all terms must match)\n" +
                                                "- Pipe `|` for OR (any term can match)\n" +
                                                "- Exclamation `!` for NOT (!crunched, !linear)\n" +
                                                "- Resolution comparisons: >2048, <1024, >=512, <=4096, =1024\n" +
                                                "- Postfix resolution: 2048< (smaller than), 2048> (bigger than)\n\n" +
                                                "Searches in: Display name, Field name, Texture name, Asset path, Format, Size, Color space");

        ActiveTab.search = EditorGUILayout.TextField(searchLabel, ActiveTab.search);

        // Advanced filters toggle
        ActiveTab.showFilterOptions = EditorGUILayout.Foldout(ActiveTab.showFilterOptions, "Advanced Filters");

        if (ActiveTab.showFilterOptions)
        {
            EditorGUI.indentLevel++;

            // Resolution filter
            EditorGUILayout.LabelField("Resolution Filter", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Min:", GUILayout.Width(30));
            ActiveTab.minResolution = EditorGUILayout.IntField(ActiveTab.minResolution, GUILayout.Width(70));
            EditorGUILayout.LabelField("Max:", GUILayout.Width(30));
            ActiveTab.maxResolution = EditorGUILayout.IntField(ActiveTab.maxResolution, GUILayout.Width(70));
            if (GUILayout.Button("Reset", GUILayout.Width(50)))
            {
                ActiveTab.minResolution = 0;
                ActiveTab.maxResolution = 8192;
            }
            EditorGUILayout.EndHorizontal();

            // Crunch filter
            EditorGUILayout.Space();
            ActiveTab.filterByCrunch = EditorGUILayout.Toggle("Filter by Crunch", ActiveTab.filterByCrunch);
            if (ActiveTab.filterByCrunch)
            {
                EditorGUI.indentLevel++;
                ActiveTab.showOnlyCrunched = EditorGUILayout.Toggle("Show Only Crunched", ActiveTab.showOnlyCrunched);
                EditorGUI.indentLevel--;
            }

            // Color space filter
            EditorGUILayout.Space();
            ActiveTab.filterByColorSpace = EditorGUILayout.Toggle("Filter by Color Space", ActiveTab.filterByColorSpace);
            if (ActiveTab.filterByColorSpace)
            {
                EditorGUI.indentLevel++;
                ActiveTab.colorSpaceFilter = (ColorSpaceFilter)EditorGUILayout.EnumPopup("Color Space:", ActiveTab.colorSpaceFilter);
                EditorGUI.indentLevel--;
            }

            // Format filter
            EditorGUILayout.Space();
            ActiveTab.filterByFormat = EditorGUILayout.Toggle("Filter by Format", ActiveTab.filterByFormat);
            if (ActiveTab.filterByFormat)
            {
                EditorGUI.indentLevel++;
                ActiveTab.formatFilter = EditorGUILayout.TextField("Format Contains:", ActiveTab.formatFilter);
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }
    }

    private void DrawTextureList()
    {
        if (ActiveTab == null) return;

        ActiveTab.scroll = EditorGUILayout.BeginScrollView(ActiveTab.scroll);

        Shader shader = ActiveTab.material.shader;
        int propCount = ShaderUtil.GetPropertyCount(shader);
        List<TextureEntry> allTextures = new();

        for (int i = 0; i < propCount; i++)
        {
            if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
            {
                string internalName = ShaderUtil.GetPropertyName(shader, i);
                string displayName = ShaderUtil.GetPropertyDescription(shader, i);
                Texture tex = ActiveTab.material.GetTexture(internalName);
                string texName = tex ? tex.name : "(null)";
                string texPath = tex ? AssetDatabase.GetAssetPath(tex) : "";

                var importInfo = GetTextureImportInfo(tex, texPath);

                if (tex != null)
                {
                    allTextures.Add(new TextureEntry
                    {
                        texture = tex,
                        textureName = texName,
                        assetPath = texPath,
                        internalName = internalName,
                        displayName = displayName,
                        importInfo = importInfo
                    });
                }
            }
        }

        IEnumerable<TextureEntry> filtered = allTextures;

        // Apply text search
        if (!string.IsNullOrWhiteSpace(ActiveTab.search))
        {
            filtered = ApplyTextSearch(filtered, ActiveTab.search);
        }

        // Apply advanced filters
        filtered = ApplyAdvancedFilters(filtered);

        // --- Display Stats ---
        int count = filtered.Count();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Textures shown: {count}");
        EditorGUILayout.Space();

        if (!filtered.Any())
        {
            EditorGUILayout.HelpBox("No matching textures found.", MessageType.Info);
        }

        foreach (var entry in filtered)
        {
            DrawTextureEntry(entry);
        }

        EditorGUILayout.EndScrollView();
    }

    private IEnumerable<TextureEntry> ApplyTextSearch(IEnumerable<TextureEntry> textures, string searchText)
    {
        if (searchText.Contains("|"))
        {
            var orTerms = searchText.Split('|').Select(term => term.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToArray();
            return textures.Where(entry => orTerms.Any(term => MatchesSearchTerm(entry, term)));
        }
        else
        {
            var andTerms = searchText.Split(',').Select(term => term.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToArray();
            return textures.Where(entry => andTerms.All(term => MatchesSearchTerm(entry, term)));
        }
    }

    private bool MatchesSearchTerm(TextureEntry entry, string term)
    {
        // Handle negation
        bool negate = false;
        string actualTerm = term;
        if (term.StartsWith("!"))
        {
            negate = true;
            actualTerm = term.Substring(1);
        }

        bool matches = false;

        // Handle resolution comparisons first
        if (TryParseResolutionComparison(actualTerm, out var comparison))
        {
            int maxRes = Math.Max(entry.texture.width, entry.texture.height);
            matches = comparison.@operator switch
            {
                ">" => maxRes > comparison.value,
                "<" => maxRes < comparison.value,
                ">=" => maxRes >= comparison.value,
                "<=" => maxRes <= comparison.value,
                "=" => maxRes == comparison.value,
                _ => false
            };
        }
        else
        {
            // Regular text search in all properties
            string lowerTerm = actualTerm.ToLower();
            matches = (entry.textureName ?? "").ToLower().Contains(lowerTerm) ||
                      (entry.assetPath ?? "").ToLower().Contains(lowerTerm) ||
                      (entry.displayName ?? "").ToLower().Contains(lowerTerm) ||
                      (entry.internalName ?? "").ToLower().Contains(lowerTerm) ||
                      entry.texture.graphicsFormat.ToString().ToLower().Contains(lowerTerm) ||
                      $"{entry.texture.width}x{entry.texture.height}".Contains(lowerTerm) ||
                      entry.texture.width.ToString().Contains(lowerTerm) ||
                      entry.texture.height.ToString().Contains(lowerTerm) ||
                      (entry.importInfo.isLinear ? "linear" : "srgb").Contains(lowerTerm) ||
                      (entry.importInfo.isCrunched ? "crunched" : "").Contains(lowerTerm) ||
                      (entry.importInfo.isNormalMap ? "normal" : "").Contains(lowerTerm) ||
                      entry.importInfo.compressionQuality.ToLower().Contains(lowerTerm);
        }

        return negate ? !matches : matches;
    }

    private bool TryParseResolutionComparison(string term, out (string @operator, int value) comparison)
    {
        comparison = default;

        // Check for prefix operators: >2048, <1024, >=512, <=4096, =1024
        if (System.Text.RegularExpressions.Regex.IsMatch(term, @"^(>=|<=|>|<|=)\d+$"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(term, @"^(>=|<=|>|<|=)(\d+)$");
            if (match.Success && int.TryParse(match.Groups[2].Value, out int value))
            {
                comparison = (match.Groups[1].Value, value);
                return true;
            }
        }

        // Check for postfix operators: 2048<, 2048>
        if (System.Text.RegularExpressions.Regex.IsMatch(term, @"^\d+(<|>)$"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(term, @"^(\d+)(<|>)$");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int value))
            {
                string op = match.Groups[2].Value == "<" ? ">" : "<";
                comparison = (op, value);
                return true;
            }
        }

        return false;
    }

    private IEnumerable<TextureEntry> ApplyAdvancedFilters(IEnumerable<TextureEntry> textures)
    {
        if (ActiveTab == null) return textures;

        var filtered = textures;

        // Resolution filter
        int minRes = Math.Max(0, ActiveTab.minResolution);
        int maxRes = Math.Max(minRes, ActiveTab.maxResolution);
        filtered = filtered.Where(entry =>
            Math.Max(entry.texture.width, entry.texture.height) >= minRes &&
            Math.Max(entry.texture.width, entry.texture.height) <= maxRes);

        // Crunch filter
        if (ActiveTab.filterByCrunch)
        {
            filtered = filtered.Where(entry => entry.importInfo.isCrunched == ActiveTab.showOnlyCrunched);
        }

        // Color space filter
        if (ActiveTab.filterByColorSpace)
        {
            filtered = ActiveTab.colorSpaceFilter switch
            {
                ColorSpaceFilter.sRGB => filtered.Where(entry => !entry.importInfo.isLinear && !entry.importInfo.isNormalMap),
                ColorSpaceFilter.Linear => filtered.Where(entry => entry.importInfo.isLinear && !entry.importInfo.isNormalMap),
                ColorSpaceFilter.NormalMaps => filtered.Where(entry => entry.importInfo.isNormalMap),
                _ => filtered
            };
        }

        // Format filter
        if (ActiveTab.filterByFormat && !string.IsNullOrWhiteSpace(ActiveTab.formatFilter))
        {
            filtered = filtered.Where(entry =>
                entry.texture.graphicsFormat.ToString().ToLower().Contains(ActiveTab.formatFilter.ToLower()));
        }

        return filtered;
    }

    private void DrawTextureEntry(TextureEntry entry)
    {
        // Add colored background for different texture types
        Color originalBGColor = GUI.backgroundColor;
        if (entry.importInfo.isLinear && !entry.importInfo.isNormalMap)
            GUI.backgroundColor = new Color(0.8f, 0.8f, 0.6f, 0.3f);
        else if (entry.importInfo.isNormalMap)
            GUI.backgroundColor = new Color(0.6f, 0.6f, 1f, 0.3f);

        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = originalBGColor;

        EditorGUILayout.BeginHorizontal();

        if (entry.texture != null)
        {
            string tooltipText = $"Path: {entry.assetPath}\nFormat: {entry.texture.graphicsFormat}\nColor Space: {(entry.importInfo.isLinear ? "Linear" : "sRGB")}";

            GUIContent texButton = new GUIContent(entry.texture, tooltipText);
            if (GUILayout.Button(texButton, GUILayout.Width(64), GUILayout.Height(64)))
            {
                EditorGUIUtility.PingObject(entry.texture);
            }
        }
        else
        {
            GUILayout.Box("No Texture", GUILayout.Width(64), GUILayout.Height(64));
        }

        EditorGUILayout.BeginVertical();

        // Display name with indicators
        string displayText = entry.displayName;
        if (entry.importInfo.isNormalMap) displayText += " [NORMAL]";
        if (entry.importInfo.isLinear && !entry.importInfo.isNormalMap) displayText += " [LINEAR]";

        GUILayout.Label($"Display Name: {displayText}", EditorStyles.wordWrappedLabel);

        Color originalColorLabel = GUI.color;
        GUI.color = Color.cyan;
        EditorGUILayout.LabelField($"Field Name: {entry.internalName}");
        GUI.color = originalColorLabel;

        EditorGUILayout.LabelField($"Texture: {entry.textureName}");
        EditorGUILayout.LabelField($"Format: {entry.texture.graphicsFormat}");
        EditorGUILayout.LabelField($"Size: {entry.texture.width}x{entry.texture.height}");
        EditorGUILayout.LabelField($"Compression: {entry.importInfo.compressionQuality}");

        if (entry.importInfo.isCrunched)
        {
            Color originalColor = GUI.color;
            GUI.color = Color.yellow;
            EditorGUILayout.LabelField("CRUNCHED");
            GUI.color = originalColor;
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3);
    }

    private TextureImportInfo GetTextureImportInfo(Texture tex, string texPath)
    {
        var info = new TextureImportInfo();

        if (tex == null || string.IsNullOrEmpty(texPath))
            return info;

        TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (importer != null)
        {
            info.isNormalMap = importer.textureType == TextureImporterType.NormalMap;
            info.isLinear = !importer.sRGBTexture;

            TextureImporterPlatformSettings platformSettings = importer.GetDefaultPlatformTextureSettings();
            info.isCrunched = platformSettings.crunchedCompression;
            info.crunchQuality = platformSettings.compressionQuality;

            // Add compression quality
            info.compressionQuality = GetCompressionQualityString(platformSettings.textureCompression);
        }

        // Fallback detection for normal maps
        if (!info.isNormalMap && tex != null)
        {
            string lowerPath = texPath.ToLower();
            string lowerName = tex.name.ToLower();
            info.isNormalMap = lowerPath.Contains("normal") || lowerPath.Contains("_n.") ||
                             lowerName.Contains("normal") || lowerName.Contains("bump") ||
                             lowerName.EndsWith("_n");
            if (info.isNormalMap)
                info.isLinear = true;
        }

        return info;
    }

    private string GetCompressionQualityString(TextureImporterCompression compression)
    {
        switch (compression)
        {
            case TextureImporterCompression.Uncompressed:
                return "None";
            case TextureImporterCompression.CompressedLQ:
                return "Low Quality";
            case TextureImporterCompression.Compressed:
                return "Normal Quality";
            case TextureImporterCompression.CompressedHQ:
                return "High Quality";
            default:
                return "";
        }
    }

    private class TextureEntry
    {
        public Texture texture;
        public string textureName;
        public string assetPath;
        public string internalName;
        public string displayName;
        public TextureImportInfo importInfo;
    }

    private class TextureImportInfo
    {
        public bool isCrunched;
        public bool isNormalMap;
        public bool isLinear;
        public int crunchQuality;
        public string compressionQuality = "";
    }
}
#endif