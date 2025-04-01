#nullable enable

using JetBrains.Annotations;
using nadena.dev.ndmf.proto.rpc;
using ResoPuppetSchema;
using UnityEngine;

namespace nadena.dev.ndmf.platform.resonite
{
    [NDMFPlatformProvider]
    internal class ResonitePlatform : INDMFPlatformProvider
    {
        internal static ResoPuppeteer.ResoPuppeteerClient _rpcClient { get; } = new Connector().Client;

        public static readonly INDMFPlatformProvider Instance = new ResonitePlatform();
        public string CanonicalName => "ndmf/resonite";
        public string DisplayName => "Resonite";
        public Texture2D? Icon => null;

        public BuildUIElement? CreateBuildUI()
        {
            return new ResoniteBuildUI();
        }

        public void InitBuildFromCommonAvatarInfo(BuildContext context, CommonAvatarInfo info)
        {
            context.GetState<ResoniteBuildState>().cai = info;
        }
    }
}