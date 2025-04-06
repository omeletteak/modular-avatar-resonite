using nadena.dev.ndmf.preview;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace nadena.dev.ndmf.platform.resonite
{
    internal class ResoniteBuildUI : BuildUIElement
    {
        const string ResourcesRoot = "Packages/nadena.dev.resonity/Editor/UI/Resources/"; 
        private UnityEngine.GameObject _avatarRoot;
        private Button _buildButton;

        public override UnityEngine.GameObject AvatarRoot
        {
            get => _avatarRoot;
            set
            {
                _avatarRoot = value;
                _buildButton?.SetEnabled(_avatarRoot != null);
            }
        }

        public ResoniteBuildUI()
        {
            // Import UXML
            var visualTree =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    ResourcesRoot + "ResoniteBuildUI.uxml");
            VisualElement rootFromUXML = visualTree.CloneTree();
            Add(rootFromUXML);

            var styleSheet =
                AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    ResourcesRoot + "ResoniteBuildUI.uss");
            styleSheets.Add(styleSheet);
            
            _buildButton = rootFromUXML.Q<Button>("build");
            _buildButton.clickable.clicked += () =>
            {
                BuildAvatar();
            };

            _buildButton.SetEnabled(_avatarRoot != null);
        }

        private void BuildAvatar()
        {
            var clone = GameObject.Instantiate(_avatarRoot);
            try
            {
                using var scope = new AmbientPlatform.Scope(ResonitePlatform.Instance);
                using var scope2 = new OverrideTemporaryDirectoryScope(null);

                var buildContext = AvatarProcessor.ProcessAvatar(clone, ResonitePlatform.Instance);

                // Find a temp path under the project root
                var tempPath = System.IO.Path.Combine(Application.temporaryCachePath, "tmp.resonitepackage");
                
                var asyncCall = ResonitePlatform._rpcClient.ConvertObjectAsync(new()
                {
                    Path = tempPath,
                    Root = new AvatarSerializer().Export(clone, buildContext.GetState<ResoniteBuildState>().cai)
                });

                var progressId = Progress.Start("Building resonite package");

                asyncCall.ResponseAsync.ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        Debug.LogException(task.Exception);
                    }
                    else
                    {
                        Debug.Log("Resonite package built successfully: " + tempPath);
                        // Put the path into the clipboard
                        NDMFSyncContext.RunOnMainThread(path =>
                        {
                            EditorGUIUtility.systemCopyBuffer = (string) path;    
                        }, tempPath);
                    }

                    Progress.Remove(progressId);
                });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clone);
            }
        }
    }
}