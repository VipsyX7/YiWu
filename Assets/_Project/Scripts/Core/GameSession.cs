namespace Klein
{
    /// <summary>
    /// 跨场景保存的会话数据。目前只有"选人界面选中的本命元素"。
    /// 场景切换不会重置静态字段，所以选人界面写入、游戏场景读取即可。
    /// </summary>
    public static class GameSession
    {
        /// <summary>玩家选中的本命元素，默认水。</summary>
        public static ElementType SelectedElement = ElementType.Water;

        /// <summary>是否走过选人流程（直接打开游戏场景时为 false）。</summary>
        public static bool HasSelection = false;

        public static void Select(ElementType e)
        {
            SelectedElement = e;
            HasSelection = true;
        }

        public static void Clear()
        {
            SelectedElement = ElementType.Water;
            HasSelection = false;
        }
    }

    /// <summary>场景名常量。必须与 Build Settings 里的名字一致。</summary>
    public static class SceneNames
    {
        public const string Title = "Title";
        public const string Select = "Select";
        public const string Game = "Whitebox";
    }
}
