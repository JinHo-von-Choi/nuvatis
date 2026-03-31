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

    /**
     * 버그 재현: 프로젝트 이름에 "NuVatis"가 포함될 때 resultMap 타입이 중복 네임스페이스를 갖는 문제.
     *
     * 시나리오:
     *  - User 클래스 실제 FQN: NuVatis.Benchmark.NuVatis.Benchmark.Core.Models.User
     *  - XML resultMap type: NuVatis.Benchmark.Core.Models.User (접두사 누락)
     *  - 수정 전: Map 메서드가 XML 타입(잘못된 FQN)을 사용 → SelectOneAsync<T> 타입 불일치 컴파일 에러
     *  - 수정 후: Map 메서드가 인터페이스 메서드 Roslyn FQN으로 폴백 → 타입 일치
     */
    [Fact]
    public void SG_ResultMap_UsesRoslynFqn_WhenXmlTypeDoesNotResolve() {
        var source = @"
using NuVatis.Attributes;
namespace NuVatis.Benchmark.NuVatis.Benchmark.Core.Models
{
    public class User
    {
        public int    Id   { get; set; }
        public string Name { get; set; }
    }
}
namespace NuVatis.Benchmark.NuVatis.Benchmark.Mappers
{
    [NuVatisMapper]
    public interface IUserMapper
    {
        NuVatis.Benchmark.NuVatis.Benchmark.Core.Models.User GetUser(int id);
    }
}";

        var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<mapper namespace=""NuVatis.Benchmark.NuVatis.Benchmark.Mappers.IUserMapper"">
  <resultMap id=""userMap"" type=""NuVatis.Benchmark.Core.Models.User"">
    <id column=""id"" property=""Id""/>
    <result column=""name"" property=""Name""/>
  </resultMap>
  <select id=""GetUser"" resultMap=""userMap"">
    SELECT id, name FROM users WHERE id = #{id}
  </select>
</mapper>";

        var (sources, _) = RunGenerator(source, xmlContent);

        var proxySource = sources.FirstOrDefault(s => s.HintName.Contains("IUserMapper"));
        Assert.False(proxySource.Equals(default),
            $"Proxy not generated. Hints: [{string.Join(", ", sources.Select(s => s.HintName))}]");

        var code = proxySource.SourceText.ToString();

        // Map 메서드는 인터페이스 메서드의 Roslyn FQN을 사용해야 한다.
        Assert.Contains("NuVatis.Benchmark.NuVatis.Benchmark.Core.Models.User Map_userMap", code);

        // XML의 잘못된 타입명(NuVatis.Benchmark.Core.Models.User)으로
        // new 객체를 생성하는 코드가 없어야 한다.
        Assert.DoesNotContain("new NuVatis.Benchmark.Core.Models.User()", code);
    }

    /**
     * XML 매퍼 statement가 Registry의 RegisterXmlStatements에 등록되어야 한다.
     * Bug: RegistryEmitter가 XML 매퍼 statement를 등록하지 않아 "Statement not found" 런타임 오류.
     */
    [Fact]
    public void SG_Registry_Contains_RegisterXmlStatements_For_XmlMapper() {
        var source = @"
using NuVatis.Attributes;
namespace TestApp.Mappers
{
    [NuVatisMapper]
    public interface IUserMapper
    {
        object GetById(int id);
        int BulkInsert(object param);
    }
}";

        var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<mapper namespace=""TestApp.Mappers.IUserMapper"">
  <select id=""GetById"">
    SELECT id, name FROM users WHERE id = #{id}
  </select>
  <insert id=""BulkInsert"">
    INSERT INTO users (name) VALUES
    <foreach collection=""users"" item=""user"" open=""("" close="")"" separator="","">
      #{user.UserName}
    </foreach>
  </insert>
</mapper>";

        var (sources, diagnostics) = RunGenerator(source, xmlContent);

        var registry = sources.FirstOrDefault(s => s.HintName.Contains("Registry"));
        Assert.False(registry.Equals(default));

        var code = registry.SourceText.ToString();

        // RegisterXmlStatements 메서드가 생성되어야 한다.
        Assert.Contains("RegisterXmlStatements", code);

        // GetById: 정적 SQL → SqlSource에 직접 삽입
        Assert.Contains("\"GetById\"", code);
        Assert.Contains("SELECT id, name FROM users WHERE id = #{id}", code);

        // BulkInsert: 동적 SQL (foreach) → DynamicSqlBuilder 람다 생성
        Assert.Contains("\"BulkInsert\"", code);
        Assert.Contains("DynamicSqlBuilder", code);

        // 람다에서 __getprop_를 통한 중첩 프로퍼티 접근이 생성되어야 한다.
        Assert.Contains("__getprop_", code);
        Assert.Contains("UserName", code);
    }

    /**
     * foreach 내 #{user.UserName} 중첩 프로퍼티 접근이
     * EmitDynamicBuilderLambda에서 __getprop_ 호출 코드로 생성되어야 한다.
     */
    [Fact]
    public void SG_EmitDynamicBuilderLambda_ForeachNestedProperty_GeneratesGetpropAccess() {
        var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<mapper namespace=""TestApp.Mappers.IUserMapper"">
  <insert id=""BulkInsert"">
    INSERT INTO users (name) VALUES
    <foreach collection=""users"" item=""user"" open=""("" close="")"" separator="","">
      #{user.UserName}
    </foreach>
  </insert>
</mapper>";

        var mapper    = NuVatis.Generators.Parsing.XmlMapperParser.Parse(xmlContent, default);
        var stmt      = mapper.Statements[0];
        var lambda    = NuVatis.Generators.Emitters.ParameterEmitter.EmitDynamicBuilderLambda(stmt.RootNode, "@");

        // 람다가 __getprop_ 헬퍼를 포함해야 한다.
        Assert.Contains("__getprop_", lambda);

        // foreach 아이템 변수(user_)에서 UserName 프로퍼티를 접근해야 한다.
        Assert.Contains("\"UserName\"", lambda);

        // 파라미터 인덱스 카운터가 생성되어야 한다.
        Assert.Contains("__idx_", lambda);

        // ParameterBinder.CreateParameter를 통해 DbParameter를 생성해야 한다.
        Assert.Contains("ParameterBinder.CreateParameter", lambda);
    }

    private static (ImmutableArray<GeneratedSourceResult> Sources, ImmutableArray<Diagnostic> Diagnostics)
        RunGenerator(string[] sources, (string fileName, string content)[]? additionalTexts = null) {

        var syntaxTrees = sources
            .Select(s => CSharpSyntaxTree.ParseText(s))
            .ToArray();

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
        if (additionalTexts is { Length: > 0 }) {
            var texts = ImmutableArray.CreateRange(
                additionalTexts.Select(t =>
                    (AdditionalText)new InMemoryAdditionalText(t.fileName, t.content)));
            driver = CSharpGeneratorDriver.Create(
                generators: new[] { generator.AsSourceGenerator() },
                additionalTexts: texts);
        } else {
            driver = CSharpGeneratorDriver.Create(
                generators: new[] { generator.AsSourceGenerator() });
        }

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var outputCompilation, out _);

        var result  = driver.GetRunResult();
        var genSources = result.Results.SelectMany(r => r.GeneratedSources).ToImmutableArray();

        var allDiagnostics = result.Diagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .ToImmutableArray();

        return (genSources, allDiagnostics);
    }

    /**
     * resultType-only select 문에서 복합 DTO 타입에 대해
     * Map_T_{FlatFqn} 메서드와 switch 분기 코드가 생성되어야 한다.
     */
    [Fact]
    public void ResultTypeOnlyStatement_GeneratesSwitchDispatchMethod() {
        var userSource = @"
namespace MyApp
{
    public class UserDto
    {
        public int    Id   { get; set; }
        public string Name { get; set; }
    }
}";

        var mapperXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<mapper namespace=""MyApp.IUserMapper"">
    <select id=""GetUser"" resultType=""MyApp.UserDto"">
        SELECT id, name FROM users WHERE id = #{id}
    </select>
</mapper>";

        var mapperInterface = @"
namespace MyApp
{
    [NuVatis.Attributes.NuVatisMapper]
    public interface IUserMapper
    {
        MyApp.UserDto GetUser(int id);
    }
}";

        var (sources, diagnostics) = RunGenerator(
            new[] { Stubs, userSource, mapperInterface },
            new[] { ("UserMapper.xml", mapperXml) });

        // SG 자체 진단(NV00x)만 에러 체크 — 생성 코드 컴파일 에러는 런타임 ref 누락이므로 제외
        var sgErrors = diagnostics.Where(d =>
            d.Severity == DiagnosticSeverity.Error &&
            d.Id.StartsWith("NV")).ToArray();
        Assert.Empty(sgErrors);

        // Map_T_XXX 메서드는 이제 공유 NuVatisTypeMappers.g.cs에 생성된다.
        var typeMappersFile = sources.FirstOrDefault(f => f.HintName == "NuVatisTypeMappers.g.cs");
        Assert.False(typeMappersFile.Equals(default),
            $"NuVatisTypeMappers.g.cs not generated. Hints: [{string.Join(", ", sources.Select(s => s.HintName))}]");

        var typeMappersCode = typeMappersFile.SourceText.ToString();
        Assert.Contains("Map_T_MyApp_UserDto", typeMappersCode);
        Assert.Contains("reader.FieldCount", typeMappersCode);
        Assert.Contains("switch (__key)", typeMappersCode);

        // 프록시는 공유 클래스의 메서드를 참조해야 한다.
        var implFile = sources.FirstOrDefault(f => f.HintName == "IUserMapperImpl.g.cs");
        Assert.False(implFile.Equals(default),
            $"IUserMapperImpl.g.cs not generated. Hints: [{string.Join(", ", sources.Select(s => s.HintName))}]");

        var implCode = implFile.SourceText.ToString();
        Assert.Contains("global::NuVatis.NuVatisTypeMappers.Map_T_MyApp_UserDto", implCode);
    }

    /**
     * resultType이 System.Int32 같은 스칼라 타입일 때
     * Map_T_* 메서드와 switch 분기 코드가 생성되어서는 안 된다.
     */
    [Fact]
    public void ResultTypeOnlyStatement_ScalarType_DoesNotGenerateSwitchMethod() {
        var mapperXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<mapper namespace=""MyApp.ICountMapper"">
    <select id=""CountUsers"" resultType=""System.Int32"">
        SELECT COUNT(*) FROM users
    </select>
</mapper>";

        var mapperInterface = @"
namespace MyApp
{
    [NuVatis.Attributes.NuVatisMapper]
    public interface ICountMapper
    {
        int CountUsers();
    }
}";

        var (sources, diagnostics) = RunGenerator(
            new[] { Stubs, mapperInterface },
            new[] { ("CountMapper.xml", mapperXml) });

        // SG 자체 진단(NV00x)만 에러 체크
        var sgErrors = diagnostics.Where(d =>
            d.Severity == DiagnosticSeverity.Error &&
            d.Id.StartsWith("NV")).ToArray();
        Assert.Empty(sgErrors);

        var implFile = sources.FirstOrDefault(f => f.HintName == "ICountMapperImpl.g.cs");
        Assert.False(implFile.Equals(default),
            $"ICountMapperImpl.g.cs not generated. Hints: [{string.Join(", ", sources.Select(s => s.HintName))}]");

        var code = implFile.SourceText.ToString();

        Assert.DoesNotContain("Map_T_System_Int32", code);
        Assert.DoesNotContain("switch (__key)", code);
    }

    [Fact]
    public void ResultTypeOnlyStatement_EnumProperty_GeneratesIntCast() {
        var enumSource = @"
namespace TestApp {
    public enum UserStatus { Active, Inactive }
    public class UserWithStatus {
        public int Id { get; set; }
        public string Name { get; set; }
        public UserStatus Status { get; set; }
    }
}";

        var mapperXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<mapper namespace=""TestApp.IStatusMapper"">
    <select id=""GetUser"" resultType=""TestApp.UserWithStatus"">
        SELECT id, name, status FROM users WHERE id = #{id}
    </select>
</mapper>";

        var mapperInterface = @"
namespace TestApp {
    [NuVatis.Attributes.NuVatisMapper]
    public interface IStatusMapper {
        TestApp.UserWithStatus GetUser(int id);
    }
}";

        var (sources, diagnostics) = RunGenerator(
            new[] { Stubs, enumSource, mapperInterface },
            new[] { ("StatusMapper.xml", mapperXml) });

        var sgErrors = diagnostics.Where(d =>
            d.Severity == DiagnosticSeverity.Error &&
            d.Id.StartsWith("NV")).ToArray();
        Assert.Empty(sgErrors);

        // Map_T_XXX 메서드는 이제 공유 NuVatisTypeMappers.g.cs에 생성된다.
        var typeMappersFile = sources.FirstOrDefault(f => f.HintName == "NuVatisTypeMappers.g.cs");
        Assert.False(typeMappersFile.Equals(default),
            $"NuVatisTypeMappers.g.cs not generated. Hints: [{string.Join(", ", sources.Select(s => s.HintName))}]");

        var typeMappersCode = typeMappersFile.SourceText.ToString();
        Assert.Contains("(TestApp.UserStatus)", typeMappersCode);
        Assert.Contains("GetInt32", typeMappersCode);
        Assert.DoesNotContain("GetFieldValue<TestApp.UserStatus>", typeMappersCode);

        // 프록시는 공유 클래스의 메서드를 참조해야 한다.
        var implFile = sources.FirstOrDefault(f => f.HintName == "IStatusMapperImpl.g.cs");
        Assert.False(implFile.Equals(default),
            $"IStatusMapperImpl.g.cs not generated. Hints: [{string.Join(", ", sources.Select(s => s.HintName))}]");

        var implCode = implFile.SourceText.ToString();
        Assert.Contains("global::NuVatis.NuVatisTypeMappers.Map_T_TestApp_UserWithStatus", implCode);
    }

    /**
     * 정상 경로 확인: XML resultMap type이 실제 FQN과 일치하면 그대로 사용.
     */
    [Fact]
    public void SG_ResultMap_UsesXmlType_WhenItResolvesCorrectly() {
        var source = @"
using NuVatis.Attributes;
namespace TestApp.Models
{
    public class Product
    {
        public int    Id   { get; set; }
        public string Name { get; set; }
    }
}
namespace TestApp.Mappers
{
    [NuVatisMapper]
    public interface IProductMapper
    {
        TestApp.Models.Product GetProduct(int id);
    }
}";

        var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<mapper namespace=""TestApp.Mappers.IProductMapper"">
  <resultMap id=""productMap"" type=""TestApp.Models.Product"">
    <id column=""id"" property=""Id""/>
    <result column=""name"" property=""Name""/>
  </resultMap>
  <select id=""GetProduct"" resultMap=""productMap"">
    SELECT id, name FROM products WHERE id = #{id}
  </select>
</mapper>";

        var (sources, _) = RunGenerator(source, xmlContent);

        var proxySource = sources.FirstOrDefault(s => s.HintName.Contains("IProductMapper"));
        Assert.False(proxySource.Equals(default));

        var code = proxySource.SourceText.ToString();

        Assert.Contains("TestApp.Models.Product Map_productMap", code);
        Assert.Contains("new TestApp.Models.Product()", code);
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
