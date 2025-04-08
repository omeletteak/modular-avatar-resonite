using nadena.dev.ndmf.proto;

namespace nadena.dev.resonity.remote.puppeteer.rpc;

internal static class Helpers
{
    internal static ObjectID? LastBone(this Finger f)
    {
        return f.Tip ?? f.Distal ?? f.Intermediate ?? f.Proximal ?? f.Metacarpal;
    }
}