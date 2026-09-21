namespace Klein
{
    /// <summary>可被治疗的单位。区域效果对「区域内所有单位」生效（含怪物，见 Gap A8）。</summary>
    public interface IHealable
    {
        void Heal(float amount);
    }

    /// <summary>可被减速的单位。腐烂区域使用。</summary>
    public interface ISlowable
    {
        /// <param name="factor">1 = 不变，0.5 = 速度减半</param>
        /// <param name="duration">持续秒数，重复施加取最强/最长</param>
        void ApplySlow(float factor, float duration);
    }
}
