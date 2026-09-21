using UnityEngine;

namespace Klein
{
    public struct ReactionResult
    {
        public bool Triggered;
        public ReactionEntry Entry;
        public ElementType UsedElement;
        public TerrainBlob Blob;
    }

    /// <summary>
    /// 元素反应仲裁。以【地形块】为单位结算。
    ///
    /// 决策 Q1：法术固有元素与玩家当前元素【两者叠加】。
    /// 仲裁顺序：先试法术固有元素，再试玩家元素；命中第一个能产生反应的元素即停止。
    /// 一次命中最多产生一次反应、最多消耗该地块 1 次反应次数。
    /// </summary>
    public static class ElementReactionResolver
    {
        public static ReactionResult Resolve(ElementGrid grid, TerrainBlob blob,
                                             ElementType innate, ElementType playerElement)
        {
            var res = new ReactionResult { Blob = blob };
            if (grid == null || blob == null || blob.Element == ElementType.None) return res;
            if (grid.OnCooldown(blob)) return res;   // 同一块地形每 60 帧最多一次

            // 1) 同元素 → 回充（不消耗，反而 +1）
            if (innate == blob.Element && innate != ElementType.None)
                return Recharge(grid, blob, innate);

            if (playerElement == blob.Element && playerElement != ElementType.None)
                return Recharge(grid, blob, playerElement);

            // 2) 异元素 → 查表（固有元素优先）
            var table = GameDatabase.I != null ? GameDatabase.I.Reactions : null;
            if (table == null) return res;

            var e = PickEntry(table, innate, blob.Element);
            if (e == null && playerElement != innate)
                e = PickEntry(table, playerElement, blob.Element);

            if (e == null) return res;
            if (blob.Depleted) return res;          // 次数为 0 时不反应（等自动回充）

            grid.Consume(blob);
            grid.MarkReacted(blob);
            res.Triggered = true;
            res.Entry = e;
            res.UsedElement = e.SpellElement;
            return res;
        }

        private static ReactionResult Recharge(ElementGrid grid, TerrainBlob blob, ElementType element)
        {
            grid.Recharge(blob);
            grid.MarkReacted(blob);
            return new ReactionResult
            {
                Triggered = true,
                Blob = blob,
                UsedElement = element,
                Entry = new ReactionEntry
                {
                    SpellElement = element,
                    TerrainElement = blob.Element,
                    Kind = ReactionKind.Recharge,
                },
            };
        }

        private static ReactionEntry PickEntry(ReactionTable table, ElementType spell, ElementType terrain)
        {
            if (spell == ElementType.None || spell == terrain) return null;
            var e = table.Find(spell, terrain);
            if (e == null || e.Kind == ReactionKind.Recharge || e.Kind == ReactionKind.None) return null;
            return e;
        }
    }
}
