using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace nadena.dev.ndmf.platform.resonite
{
    public class ResoniteBatchBuildUI : EditorWindow
    {
        private const string ResourcesRoot = "Packages/nadena.dev.modular-avatar.resonite/Editor/UI/Resources/";

        private ListView _avatarListView;
        private Button _addSelectedButton;
        private Button _buildAllButton;
        private Label _statusLabel;

        private readonly List<GameObject> _avatarsToBuild = new List<GameObject>();

        [MenuItem("Modular Avatar/Batch Build for Resonite")]
        public static void ShowWindow()
        { 
            GetWindow<ResoniteBatchBuildUI>("Resonite Batch Build");
        }

        public void CreateGUI()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ResourcesRoot + "ResoniteBatchBuildUI.uxml");
            VisualElement root = visualTree.CloneTree();
            rootVisualElement.Add(root);

            _avatarListView = root.Q<ListView>("avatar-list");
            _addSelectedButton = root.Q<Button>("add-selected-button");
            _buildAllButton = root.Q<Button>("build-all-button");
            _statusLabel = root.Q<Label>("status-label");

            _avatarListView.makeItem = () => new Label();
            _avatarListView.bindItem = (element, i) => (element as Label).text = _avatarsToBuild[i].name;
            _avatarListView.itemsSource = _avatarsToBuild;
            _avatarListView.selectionType = SelectionType.Multiple;

            _addSelectedButton.clicked += AddSelectedPrefabs;
            _buildAllButton.clicked += BuildAll;
            
            root.RegisterCallback<DragUpdatedEvent>(OnDragUpdate);
            root.RegisterCallback<DragPerformEvent>(OnDragPerform);

            _statusLabel.text = "Drag and drop prefabs or select them and click 'Add'.";
        }

        private void AddSelectedPrefabs()
        {
            int count = 0;
            foreach (var obj in Selection.gameObjects)
            {
                if (PrefabUtility.IsPartOfPrefabAsset(obj) && !_avatarsToBuild.Contains(obj))
                {
                    _avatarsToBuild.Add(obj);
                    count++;
                }
            }
            
            if (count > 0)
            {
                _avatarListView.Rebuild();
                _statusLabel.text = $"Added {count} prefabs. Total: {_avatarsToBuild.Count}";
            }
        }

        private void BuildAll()
        {
            _statusLabel.text = $"Attempting to build {_avatarsToBuild.Count} avatars...";
            // TODO: Connect to BatchBuildController to run the build process.
            Debug.Log($"BuildAll button clicked. {_avatarsToBuild.Count} avatars in list.");
            foreach(var avatar in _avatarsToBuild)
            {
                Debug.Log($"  - {avatar.name}");
            }
        }
        
        private void OnDragUpdate(DragUpdatedEvent evt)
        {
            bool containsPrefabs = false;
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is GameObject go && PrefabUtility.IsPartOfPrefabAsset(go))
                {
                    containsPrefabs = true;
                    break;
                }
            }

            if (containsPrefabs)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            }
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            int count = 0;
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is GameObject go && PrefabUtility.IsPartOfPrefabAsset(go) && !_avatarsToBuild.Contains(go))
                {
                    _avatarsToBuild.Add(go);
                    count++;
                }
            }

            if (count > 0)
            {
                _avatarListView.Rebuild();
                _statusLabel.text = $"Added {count} prefabs via drag and drop. Total: {_avatarsToBuild.Count}";
            }
        }
    }
}