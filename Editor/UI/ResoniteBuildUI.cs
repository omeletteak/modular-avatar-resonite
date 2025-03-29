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

                var asyncCall = ResonitePlatform._rpcClient.ConvertObjectAsync(new()
                {
                    Path = "d:\\test.resonitepackage",
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
                        Debug.Log("Resonite package built successfully");
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