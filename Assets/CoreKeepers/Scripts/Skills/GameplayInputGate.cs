using UnityEngine.EventSystems;

namespace CoreKeepers
{
    public static class GameplayInputGate
    {
        private static int consumedPointerFrame = -1;
        public static bool IsPointerOverUi => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        public static bool IsModalOpen =>
            (CoreRadialMenu.Instance != null && CoreRadialMenu.Instance.IsOpen) ||
            (SkillUpgradePopupUI.Instance != null && SkillUpgradePopupUI.Instance.IsOpen);
        public static bool IsPointerBlocked => IsPointerOverUi || IsModalOpen || consumedPointerFrame == UnityEngine.Time.frameCount;
        public static void ConsumeGameplayPointer() => consumedPointerFrame = UnityEngine.Time.frameCount;
    }
}
