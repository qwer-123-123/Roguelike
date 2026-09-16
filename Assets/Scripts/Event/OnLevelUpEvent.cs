namespace Game
{
    /// <summary>升级事件（struct 减少 GC）：携带三选一选项，供 UI 面板订阅。</summary>
    public struct OnLevelUpEvent
    {
        public UpgradeChoice[] Choices;
    }
}
