using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace NuVatis.Generators.Tests;

/**
 * Source Generator 통합 테스트.
 * CSharpGeneratorDriver로 실제 SG를 실행하여
 * 생성된 코드가 올바른 구조를 갖는지 검증한다.
 *
 * @author 최진호
 * @date   2026-02-25
 */
public class GeneratorIntegrationTests {

    private const string Stubs = @"
namespace NuVatis.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Interface)]
    public sealed class NuVatisMapperAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class SelectAttribute : System.Attribute
    {
        public string Sql { get; }
        public SelectAttribute(string sql) => Sql = sql;
    }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class InsertAttribute : System.Attribute
    {
        public string Sql { get; }
        public InsertAttribute(string sql) => Sql = sql;
    }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class UpdateAttribute : System.Attribute
    {
        public string Sql { get; }
        public UpdateAttribute(string sql) => Sql = sql;
    }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class DeleteAttribute : System.Attribute
    {
        public string Sql { get; }
        public DeleteAttribute(string sql) => Sql = sql;
    }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class ResultMapAttribute : System.Attribute
    {
        public string ResultMapId { get; }
        public ResultMapAttribute(string id) => ResultMapId = id;
    }
}

namespace NuVatis.Session
{
    public interface ISqlSession : System.IDisposable, System.IAsyncDisposable
    {
        T SelectOne<T>(string statementId, object parameter = null);
        System.Threading.Tasks.Task<T> SelectOneAsync<T>(string statementId, object parameter = null, System.Threading.CancellationToken ct = default);
        System.Collections.Generic.IList<T> SelectList<T>(string statementId, object parameter = null);
        System.Threading.Tasks.Task<System.Collections.Generic.IList<T>> SelectListAsync<T>(string statementId, object parameter = null, System.Threading.CancellationToken ct = default);
        int Insert(string statementId, object parameter = null);
        System.Threading.Tasks.Task<int> InsertAsync(string statementId, object parameter = null, System.Threading.CancellationToken ct = default);
        int Update(string statementId, object parameter = null);
        System.Threading.Tasks.Task<int> UpdateAsync(string statementId, object parameter = null, System.Threading.CancellationToken ct = default);
        int Delete(string statementId, object parameter = null);
        System.Threading.Tasks.Task<int> DeleteAsync(string statementId, object parameter = null, System.Threading.CancellationToken ct = default);
        void Dispose();
        System.Threading.Tasks.ValueTask DisposeAsync();
    }
}
";

    private static (ImmutableArray<GeneratedSourceResult> Sources, ImmutableArray<Diagnostic> Diagnostics)
        RunGenerator(string userSource, string? xmlContent = null) {

        var syntaxTrees = new[] {
            CSharpSyntaxTree.ParseText(Stubs),
            CSharpSyntaxTree.ParseText(userSource)
        };

        var references = new MetadataReference[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Threading.Tasks").Location),
            MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new NuVatisIncrementalGenerator();

        GeneratorDriver driver;
        if (xmlContent is not null) {
            var additionalTexts = ImmutableArray.Create<AdditionalText>(
                new InMemoryAdditionalText("Mappers/Test.xml", xmlContent));
            driver = CSharpGeneratorDriver.Create(
                generators: new[] { generator.AsSourceGenerator() },
                additionalTexts: additionalTexts);
        } else {
            driver = CSharpGeneratorDriver.Create(
                generators: new[] { generator.AsSourceGenerator() });
        }

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var outputCompilation, out _);

        var result  = driver.GetRunResult();
        var sources = result.Results.SelectMany(r => r.GeneratedSources).ToImmutableArray();

        var allDiagnostics = result.Diagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .ToImmutableArray();

        return (sources, allDiagnostics);
    }

    [Fact]
    public void SG_Driver_Runs_And_Produces_Sources() {
        var source = @"
using NuVatis.Attributes;
namespace TestApp.Mappers
{
    [NuVatisMapper]
    public interface ISimpleMapper
    {
        [Select(""SELECT 1"")]
        object GetOne();
    }
}";

        var (sources, _) = RunGenerator(source);

        Assert.True(sources.Length >= 2,
            $"Expected >= 2 generated sources (proxy + registry), got {sources.Length}. " +
            $"Hints: [{string.Join(", ", sources.Select(s => s.HintName))}]");
    }

    [Fact]
    public void SG_Generates_Proxy_With_Correct_Class_Name() {
        var source = @"
using NuVatis.Attributes;
namespace TestApp.Mappers
{
    [NuVatisMapper]
    public interface IUserMapper
    {
        [Select(""SELECT * FROM users WHERE id = #{Id}"")]
        object GetById(int id);

        [Insert(""INSERT INTO users (name) VALUES (#{Name})"")]
        int Insert(object user);
    }
}";

        var (sources, _) = RunGenerator(source);

        var proxySource = sources.FirstOrDefault(s => s.HintName.Contains("IUserMapper"));
        Assert.False(proxySource.Equals(default),
            $"No proxy generated for IUserMapper. Hints: [{string.Join(", ", sources.Select(s => s.HintName))}]");

        var code = proxySource.SourceText.ToString();
        Assert.Contains("class UserMapperImpl", code);
        Assert.Contains("ISqlSession", code);
        Assert.Contains("GetById", code);
        Assert.Contains("Insert", code);
    }

    [Fact]
    public void SG_Generates_Registry_Referencing_All_Mappers() {
        var source = @"
using NuVatis.Attributes;
namespace TestApp.Mappers
{
    [NuVatisMapper]
    public interface IOrderMapper
    {
        [Select(""SELECT * FROM orders"")]
        object GetAll();
    }
}";

        var (sources, _) = RunGenerator(source);

        var registry = sources.FirstOrDefault(s => s.HintName.Contains("Registry"));
        Assert.False(registry.Equals(default),
            $"No registry generated. Hints: [{string.Join(", ", sources.Select(s => s.HintName))}]");

        var code = registry.SourceText.ToString();
        Assert.Contains("NuVatisMapperRegistry", code);
        Assert.Contains("IOrderMapper", code);
    }

    [Fact]
    public void SG_Only_Processes_NuVatisMapper_Interfaces() {
        var source = @"
namespace External
{
    public interface IMapper { }
    public interface IProjectionMapper { }
}
namespace TestApp.Mappers
{
    using NuVatis.Attributes;

    [NuVatisMapper]
    public interface IProductMapper
    {
        [Select(""SELECT * FROM products"")]
        object GetAll();
    }
}";

        var (sources, _) = RunGenerator(source);

        var proxyHints = sources.Select(s => s.HintName).ToArray();
        Assert.DoesNotContain("MapperImpl.g.cs", proxyHints);
        Assert.DoesNotContain("ProjectionMapperImpl.g.cs", proxyHints);
        Assert.Contains("IProductMapperImpl.g.cs", proxyHints);
    }

    [Fact]
    public void SG_Generates_All_CRUD_Method_Delegations() {
        var source = @"
using NuVatis.Attributes;
namespace TestApp.Mappers
{
    [NuVatisMapper]
    public interface ICategoryMapper
    {
        [Select(""SELECT * FROM categories WHERE id = #{Id}"")]
        object GetById(int id);

        [Select(""SELECT * FROM categories"")]
        System.Collections.Generic.IList<object> GetAll();

        [Insert(""INSERT INTO categories (name) VALUES (#{Name})"")]
        int Insert(object category);

        [Update(""UPDATE categories SET name = #{Name} WHERE id = #{Id}"")]
        int Update(object category);

        [Delete(""DELETE FROM categories WHERE id = #{Id}"")]
        int Delete(int id);
    }
}";

        var (sources, _) = RunGenerator(source);

        var proxy = sources.FirstOrDefault(s => s.HintName.Contains("ICategoryMapper"));
        Assert.False(proxy.Equals(default));

        var code = proxy.SourceText.ToString();
        Assert.Contains("SelectOne", code);
        Assert.Contains("SelectList", code);
        Assert.Contains("GetById", code);
        Assert.Contains("GetAll", code);
        Assert.Contains("Insert", code);
        Assert.Contains("Update", code);
        Assert.Contains("Delete", code);
    }

    [Fact]
    public void SG_Reports_NV004_For_StringSubstitution_In_XML() {
        var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<mapper namespace=""TestApp.Mappers.IDangerousMapper"">
  <select id=""GetByTable"">
    SELECT * FROM ${tableName} WHERE id = #{Id}
  </select>
</mapper>";

        var interfaceSource = @"
using NuVatis.Attributes;
namespace TestApp.Mappers
{
    [NuVatisMapper]
    public interface IDangerousMapper
    {
        object GetByTable(object param);
    }
}";

        var (sources, diagnostics) = RunGenerator(interfaceSource, xmlContent);

        var nv004 = diagnostics.Where(d => d.Id == "NV004").ToArray();
        Assert.Single(nv004);
        Assert.Contains("tableName", nv004[0].GetMessage());
    }
}

/**
 * 인메모리 AdditionalText 구현. SG 테스트에서 XML 파일을 주입할 때 사용.
 */
internal sealed class InMemoryAdditionalText : AdditionalText {
    private readonly string _text;

    public InMemoryAdditionalText(string path, string text) {
        Path  = path;
        _text = text;
    }

    public override string Path { get; }

    public override SourceText? GetText(CancellationToken cancellationToken = default) {
        return SourceText.From(_text);
    }
}
