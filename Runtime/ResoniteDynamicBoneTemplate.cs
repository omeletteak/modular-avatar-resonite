using JetBrains.Annotations;
using UnityEngine;

namespace nadena.dev.resonity.platform.resonite.runtime
{
    [PublicAPI]
    public class ResoniteDynamicBoneTemplate : ScriptableObject
    {
        [SerializeField] private float m_Inertia = 0.2f;
        public float Inertia { get => m_Inertia; set => m_Inertia = value; }

        [SerializeField] private float m_InertiaForce = 2.0f;
        public float InertiaForce { get => m_InertiaForce; set => m_InertiaForce = value; }

        [SerializeField] private float m_Damping = 5.0f;
        public float Damping { get => m_Damping; set => m_Damping = value; }

        [SerializeField] private float m_Elasticity = 100f;
        public float Elasticity { get => m_Elasticity; set => m_Elasticity = value; }

        [SerializeField] private float m_Stiffness = 0.2f;
        public float Stiffness { get => m_Stiffness; set => m_Stiffness = value; }

        [SerializeField] private float m_StretchRestoreSpeed = 6f;
        public float StretchRestoreSpeed { get => m_StretchRestoreSpeed; set => m_StretchRestoreSpeed = value; }
    }
}