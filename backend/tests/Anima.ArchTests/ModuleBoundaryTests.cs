using System.Reflection;
using NetArchTest.Rules;

namespace Anima.ArchTests;

/// <summary>
/// Ranh giới module của modular monolith (SAD 2, 4.1). Module chỉ được phụ thuộc module khác theo bảng dưới;
/// phụ thuộc phải đi qua interface/DTO công khai, không dùng lớp cài đặt (*Service, *Seeder) của module khác.
/// </summary>
public class ModuleBoundaryTests
{
    private static readonly Dictionary<string, (Assembly Asm, string[] Allowed)> Modules = new()
    {
        ["Identity"] = (typeof(Anima.Identity.IdentityModule).Assembly, []),
        ["Economy"] = (typeof(Anima.Economy.EconomyModule).Assembly, []),
        ["Catalog"] = (typeof(Anima.Catalog.CatalogModule).Assembly, []),
        ["Fairness"] = (typeof(Anima.Fairness.FairnessModule).Assembly, ["Identity"]),
        ["Wallet"] = (typeof(Anima.Wallet.WalletModule).Assembly, ["Economy", "Identity"]),
        ["Collection"] = (typeof(Anima.Collection.CollectionModule).Assembly, ["Catalog"]),
        ["Gacha"] = (typeof(Anima.Gacha.GachaModule).Assembly, ["Fairness", "Wallet", "Catalog", "Collection", "Economy", "Identity"]),
        ["Forge"] = (typeof(Anima.Forge.ForgeModule).Assembly, ["Fairness", "Wallet", "Gacha", "Catalog", "Collection", "Economy", "Identity"]),
        ["Quest"] = (typeof(Anima.Quest.QuestModule).Assembly, ["Identity", "Gacha", "Wallet"]),
        ["Admin"] = (typeof(Anima.Admin.AdminModule).Assembly, ["Identity", "Catalog", "Economy", "Wallet"]),
    };

    private static string[] AnimaRefs(Assembly a) => a.GetReferencedAssemblies().Select(r => r.Name!).Where(n => n.StartsWith("Anima.", StringComparison.Ordinal)).ToArray();
    private static string Name(string module) => $"Anima.Modules.{module}";

    public static IEnumerable<object[]> ModuleNames => Modules.Keys.Select(k => new object[] { k });

    [Theory(DisplayName = "Module chỉ tham chiếu các module được phép")]
    [MemberData(nameof(ModuleNames))]
    public void ModuleReferencesAreAllowed(string module)
    {
        var (asm, allowed) = Modules[module];
        var allowedNames = allowed.Select(Name).Concat(["Anima.SharedKernel", "Anima.Contracts"]).ToHashSet();
        var illegal = AnimaRefs(asm).Where(r => !allowedNames.Contains(r)).ToList();
        Assert.True(illegal.Count == 0, $"{module} tham chiếu trái phép: {string.Join(", ", illegal)}");
    }

    [Fact(DisplayName = "Đồ thị phụ thuộc giữa các module không có vòng")]
    public void NoCycles()
    {
        var visiting = new HashSet<string>(); var done = new HashSet<string>();
        void Visit(string m)
        {
            if (done.Contains(m)) return;
            Assert.True(visiting.Add(m), $"Vòng phụ thuộc quanh {m}");
            foreach (var d in Modules[m].Allowed) Visit(d);
            visiting.Remove(m); done.Add(m);
        }
        foreach (var m in Modules.Keys) Visit(m);
    }

    [Theory(DisplayName = "Không dùng lớp cài đặt (*Service, *Seeder, *Module) của module khác")]
    [MemberData(nameof(ModuleNames))]
    public void NoDependencyOnOtherModulesImplementation(string module)
    {
        var (asm, _) = Modules[module];
        var forbidden = Modules.Where(kv => kv.Key != module)
            .SelectMany(kv => kv.Value.Asm.GetTypes().Where(t => t.IsClass && (t.Name.EndsWith("Service") || t.Name.EndsWith("Seeder") || t.Name.EndsWith("Module")) && !t.IsNested).Select(t => t.FullName!))
            .ToArray();
        var result = Types.InAssembly(asm).That().ResideInNamespaceStartingWith($"Anima.{module}").ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
        Assert.True(result.IsSuccessful, $"{module} phụ thuộc cài đặt của module khác: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact(DisplayName = "SharedKernel không phụ thuộc module nào; Contracts không phụ thuộc gì ngoài netstandard")]
    public void KernelAndContractsAreLeaves()
    {
        var kernel = typeof(Anima.SharedKernel.DomainException).Assembly;
        Assert.Empty(AnimaRefs(kernel).Except(["Anima.Contracts"]));              // trình biên dịch bỏ tham chiếu không dùng, nên chỉ kiểm tra tập con
        var contracts = typeof(Anima.Contracts.ErrorCodes).Assembly;
        Assert.Empty(AnimaRefs(contracts));
        Assert.DoesNotContain(contracts.GetReferencedAssemblies(), r => r.Name!.StartsWith("Microsoft.", StringComparison.Ordinal) || r.Name == "Npgsql");
    }

    [Theory(DisplayName = "NFR-12: module không dùng System.Random (kết quả ngẫu nhiên phải qua CSPRNG)")]
    [MemberData(nameof(ModuleNames))]
    public void NoInsecureRandom(string module)
    {
        var result = Types.InAssembly(Modules[module].Asm).ShouldNot().HaveDependencyOn("System.Random").GetResult();
        Assert.True(result.IsSuccessful, $"{module} dùng System.Random: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact(DisplayName = "Mọi module đăng ký migration SQL nhúng")]
    public void EveryModuleHasMigrations()
    {
        foreach (var (name, (asm, _)) in Modules)
            Assert.Contains(asm.GetManifestResourceNames(), r => r.Contains(".Migrations.") && r.EndsWith(".sql", StringComparison.Ordinal));
    }
}
