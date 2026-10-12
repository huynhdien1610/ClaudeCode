namespace Anima.Contracts
{

    /// <summary>
    /// Kỹ năng loại Passive của thẻ (docs/SKILLS_ANIMA.md). Chỉ Anima giữ Passive mới có Ký ức phong ấn (BR-CARD-05).
    /// Module Memory và Catalog không tham chiếu Battle nên danh sách nằm ở đây; test tích hợp kiểm nó khớp danh mục kỹ năng của máy đấu.
    /// </summary>
    public static class PassiveSkills
    {
        public static readonly string[] Ids =
        {
        "luminara.radiant_aura", "umbryx.veil_of_sorrow", "pyraxis.rage_bound", "aqualis.deep_calm",
        "terrakin.unyielding", "ventara.free_spirit", "voltaris.overcharge", "nihilum.emptiness",
    };

        public static bool IsPassive(string? skillId) => skillId != null && System.Array.IndexOf(Ids, skillId) >= 0;
    }
}
