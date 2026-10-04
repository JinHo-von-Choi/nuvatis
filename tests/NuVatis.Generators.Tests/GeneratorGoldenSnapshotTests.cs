using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace NuVatis.Generators.Tests;

/**
 * 생성 코드의 전체 텍스트를 고정하는 회귀 스냅샷.
 *
 * 기존 GeneratorIntegrationTests는 Assert.Contains(부분 문자열)로만 검증해
 * 공백·변수명·SQL 조립 순서가 바뀌어도 통과한다. 에미터 내부 구조를 리팩터링할 때는
 * 생성 출력이 1글자도 달라지지 않아야 하므로 전체 출력을 해시로 고정한다.
 *
 * 의도한 변경일 때만 REGRESSION_BASELINE=1 로 실행하면 현재 해시를 출력한다.
 */
public class GeneratorGoldenSnapshotTests {

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

    private const string SrcAttributeBasic = @"
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

        [Update(""UPDATE users SET name = #{Name} WHERE id = #{Id}"")]
        int Update(object user);

        [Delete(""DELETE FROM users WHERE id = #{Id}"")]
        int Delete(int id);
    }
}";

    private const string SrcXmlDynamic = @"
using NuVatis.Attributes;
namespace TestApp.Models
{
    public class OrderQuery
    {
        public string? Name   { get; set; }
        public int?    MinAge { get; set; }
        public string? Status { get; set; }
        public string? SortBy { get; set; }
        public int[]?  Ids    { get; set; }
    }
}
namespace TestApp.Mappers
{
    [NuVatisMapper]
    public interface IOrderMapper
    {
        object Search(TestApp.Models.OrderQuery param);
        object FindAll();
        object Count(TestApp.Models.OrderQuery param);
        object ListByIds(TestApp.Models.OrderQuery param);
        object UpdatePartial(TestApp.Models.OrderQuery param);
    }
}";

    private const string XmlDynamic = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<mapper namespace=""TestApp.Mappers.IOrderMapper"">
  <select id=""Search"" resultType=""object"">
    SELECT * FROM orders
    <where>
      <if test=""Name != null"">AND name = #{Name}</if>
      <if test=""MinAge > 0"">AND age &gt;= #{MinAge}</if>
      <if test=""Status == 'open'"">AND status = 'open'</if>
    </where>
    <choose>
      <when test=""SortBy == 'name'"">ORDER BY name</when>
      <when test=""SortBy == 'age'"">ORDER BY age DESC</when>
      <otherwise>ORDER BY id</otherwise>
    </choose>
  </select>
  <select id=""FindAll"" resultType=""object"">SELECT * FROM orders</select>
  <select id=""Count"" resultType=""object"">
    SELECT COUNT(*) FROM orders
    <where><if test=""Name != null"">AND name = #{Name}</if></where>
  </select>
  <select id=""ListByIds"" resultType=""object"">
    SELECT * FROM orders WHERE id IN
    <foreach collection=""Ids"" item=""id"" open=""("" close="")"" separator="","">#{id}</foreach>
  </select>
  <update id=""UpdatePartial"">
    UPDATE orders
    <set>
      <if test=""Name != null"">name = #{Name},</if>
      <if test=""Total != null"">total = #{Total},</if>
    </set>
    WHERE id = #{Id}
  </update>
</mapper>";

    private const string SrcXmlNested = @"
using NuVatis.Attributes;
namespace TestApp.Mappers
{
    [NuVatisMapper]
    public interface IItemMapper
    {
        object InsertBatch(object param);
        object SearchNested(object param);
    }
}";

    private const string XmlNested = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<mapper namespace=""TestApp.Mappers.IItemMapper"">
  <insert id=""InsertBatch"">
    INSERT INTO items (name, qty) VALUES
    <foreach collection=""Items"" item=""it"" separator="","">
      (#{it.Name}, #{it.Qty})
    </foreach>
  </insert>
  <select id=""SearchNested"" resultType=""object"">
    SELECT * FROM items
    <where>
      <if test=""Tags != null"">
        AND tag IN
        <foreach collection=""Tags"" item=""t"" open=""("" close="")"" separator="","">#{t}</foreach>
      </if>
    </where>
  </select>
</mapper>";

    private const string SrcXmlSubstitution = @"
using NuVatis.Attributes;
namespace TestApp.Mappers
{
    [NuVatisMapper]
    public interface IReportMapper
    {
        object ByColumn(object param);
        object Raw(object param);
    }
}";

    private const string XmlSubstitution = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<mapper namespace=""TestApp.Mappers.IReportMapper"">
  <select id=""ByColumn"" resultType=""object"">SELECT * FROM report ORDER BY ${SortColumn}</select>
  <select id=""Raw"" resultType=""object"">SELECT * FROM report WHERE name = ${Name} AND age > #{Age}</select>
</mapper>";

    private static readonly (string Name, string Source, string? Xml)[] Corpus = {
        ("attribute-basic",     SrcAttributeBasic,     null),
        ("xml-dynamic-tags",    SrcXmlDynamic,          XmlDynamic),
        ("xml-nested-foreach",  SrcXmlNested,           XmlNested),
        ("xml-string-subst",    SrcXmlSubstitution,     XmlSubstitution),
    };

    private static string GenerateAll() {
        var sb = new StringBuilder();

        foreach (var (name, source, xml) in Corpus) {
            var syntaxTrees = new[] {
                CSharpSyntaxTree.ParseText(Stubs),
                CSharpSyntaxTree.ParseText(source)
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
                "GoldenAssembly_" + name, syntaxTrees, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var sg = new NuVatisIncrementalGenerator().AsSourceGenerator();

            GeneratorDriver driver = xml is null
                ? CSharpGeneratorDriver.Create(generators: new[] { sg })
                : CSharpGeneratorDriver.Create(
                    generators: new[] { sg },
                    additionalTexts: ImmutableArray.Create<AdditionalText>(
                        new InMemoryAdditionalText("Mappers/Test.xml", xml)));

            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
            var result = driver.GetRunResult();

            sb.Append("########## CASE: ").Append(name).Append('\n');

            foreach (var diag in result.Diagnostics) sb.Append("DIAG: ").Append(diag).Append('\n');
            foreach (var gen in result.Results.SelectMany(r => r.GeneratedSources)
                                             .OrderBy(s => s.HintName, StringComparer.Ordinal)) {
                sb.Append("--- ").Append(gen.HintName).Append(" ---\n");
                sb.Append(gen.SourceText.ToString().ReplaceLineEndings("\n"));
            }
        }
        return sb.ToString();
    }

    private static string Hash(string text) {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    [Fact]
    public void GeneratedOutput_IsByteIdenticalToBaseline() {
        var output = GenerateAll();
        var actual  = Hash(output);

        if (Environment.GetEnvironmentVariable("REGRESSION_BASELINE") == "1") {
            Console.WriteLine($"[BASELINE] hash = {actual}");
            return;
        }

        const string expected = "14db368de9a452b7c290920aac07ed063ecbb4e3691084be9d5bcd43bbe17db9";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GeneratedOutput_ActuallyCoversEmitterPaths() {        var output = GenerateAll();

        // 모든 코퍼스가 방출 경로를 태웠는지 확인 — 비어 있으면 해시만 통과하는 사고를 막는다
        Assert.DoesNotContain("CS8785", output);          // SG 예외가 없어야 함
        Assert.Contains("BuildSql_", output);             // ProxyEmitter 정적 방출 경로
        Assert.Contains("__sb_", output);                 // 람다/정적 SQL 빌더 본문
        Assert.Contains("NuVatisMapperRegistry", output);
        Assert.Contains("NuVatisTypeMappers", output);
        Assert.Contains("ORDER BY", output);
        Assert.Contains("INSERT INTO items", output);
        Assert.Contains("COUNT(*)", output);
        Assert.True(output.Length > 40000, $"생성 출력이 지나치게 작다: {output.Length}자");
    }

    /**
     * 코퍼스가 정적 타입 직접 접근과 리플렉션 폴백을 **둘 다** 태우는지 보장한다.
     *
     * 파라미터 타입이 구체 클래스인 경우에만 직접 접근(__typed_)이 발동하므로,
     * 코퍼스 구성이 한쪽으로 쏠리면 어느 경로의 회귀를 골든 스냅샷이 놓친다.
     */
    [Fact]
    public void GeneratedOutput_CoversBothTypedAndReflectionPaths() {
        var output = GenerateAll();

        // 직접 접근 경로 — 파라미터 타입이 구체 클래스인 statement
        Assert.Contains("__typed_ = __param_ is global::TestApp.Models.OrderQuery", output);
        Assert.Contains("__typed_?.Name", output);
        Assert.Contains("__typed_?.MinAge", output);
        Assert.Contains("__typed_?.Ids", output);   // foreach 컬렉션 접근

        // 리플렉션 폴백 경로 — 파라미터 타입이 object 인 statement + 중첩 경로
        Assert.Contains("static object? __getprop_(object? o_, string n_)", output);
        Assert.Contains("__getprop_(__param_, ", output);
        Assert.Contains("__getprop_(it_, \"Name\")", output);   // foreach 아이템 프로퍼티
    }

    /**
     * 직접 접근이 발동한 statement에는 남은 리플렉션 호출이 없어야 한다.
     * (조회로 전부 바뀌지 않았다면 최적화가 절반만 먹은 것이다)
     */
    [Fact]
    public void GeneratedOutput_TypedStatementsHaveNoResidualReflectionOnSameProperties() {
        // 파라미터 타입이 구체 클래스인 케이스만 검사한다.
        // object 파라미터 코퍼스(xml-string-subst 등)는 폴백이 정상이므로 제외해야 한다.
        var typedCase = ExtractCase(GenerateAll(), "xml-dynamic-tags");

        Assert.Contains("__typed_?.Name", typedCase);
        Assert.DoesNotContain("__getprop_(__param_, \"Name\")", typedCase);
        Assert.DoesNotContain("__getprop_(__param_, \"MinAge\")", typedCase);
        Assert.DoesNotContain("__getprop_(__param_, \"Status\")", typedCase);
        Assert.DoesNotContain("__getprop_(__param_, \"SortBy\")", typedCase);
    }

    [Fact]
    public void GeneratedOutput_ObjectParameterCaseStaysOnReflection() {
        // object 파라미터 statement 는 리플렉션에 남아 있어야 한다 — 폴백 계약
        var objectCase = ExtractCase(GenerateAll(), "xml-string-subst");

        Assert.DoesNotContain("__typed_", objectCase);
        Assert.Contains("__getprop_(__param_, \"Age\")", objectCase);
    }

    /** 전체 덤프에서 특정 코퍼스 구간만 잘라낸다. */
    private static string ExtractCase(string dump, string caseName) {
        const string marker = "########## CASE: ";
        var start = dump.IndexOf(marker + caseName, StringComparison.Ordinal);
        Assert.True(start >= 0, $"코퍼스를 찾지 못했다: {caseName}");

        var from = dump.IndexOf('\n', start) + 1;
        var next = dump.IndexOf(marker, from, StringComparison.Ordinal);
        return next < 0 ? dump[from..] : dump[from..next];
    }
}
