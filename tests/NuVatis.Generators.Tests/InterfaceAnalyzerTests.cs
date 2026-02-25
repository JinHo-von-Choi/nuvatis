using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NuVatis.Generators.Analysis;
using Xunit;

namespace NuVatis.Generators.Tests;

/**
 * InterfaceAnalyzer 단위 테스트.
 *
 * [NuVatisMapper] 어트리뷰트 또는 NuVatis SQL 어트리뷰트 기반
 * opt-in 필터링이 정확히 동작하는지 검증한다.
 *
 * @author 최진호
 * @date   2026-02-25
 */
public class InterfaceAnalyzerTests {

    private static readonly string NuVatisAttributesSources = @"
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
}
";

    [Fact]
    public void InterfaceWithNuVatisMapperAttribute_IsDetected() {
        var source = @"
using NuVatis.Attributes;

namespace MyApp.Mappers
{
    [NuVatisMapper]
    public interface IUserMapper
    {
        System.Threading.Tasks.Task<object> GetUserById(int id);
    }
}
";
        var compilation = CreateCompilation(source);
        var interfaces  = InterfaceAnalyzer.FindMapperInterfaces(compilation, CancellationToken.None);

        Assert.Single(interfaces);
        Assert.Equal("IUserMapper", interfaces[0].Name);
    }

    [Fact]
    public void InterfaceWithNuVatisSqlAttribute_IsDetected() {
        var source = @"
using NuVatis.Attributes;

namespace MyApp.Mappers
{
    public interface IOrderMapper
    {
        [Select(""SELECT * FROM orders WHERE id = #{id}"")]
        System.Threading.Tasks.Task<object> GetOrderById(int id);
    }
}
";
        var compilation = CreateCompilation(source);
        var interfaces  = InterfaceAnalyzer.FindMapperInterfaces(compilation, CancellationToken.None);

        Assert.Single(interfaces);
        Assert.Equal("IOrderMapper", interfaces[0].Name);
    }

    [Fact]
    public void InterfaceWithMapperSuffix_WithoutAttribute_IsNotDetected() {
        var source = @"
namespace AutoMapper
{
    public interface IMapper
    {
        object Map(object source);
    }

    public interface IProjectionMapper
    {
        object Project(object source);
    }
}
";
        var compilation = CreateCompilation(source);
        var interfaces  = InterfaceAnalyzer.FindMapperInterfaces(compilation, CancellationToken.None);

        Assert.Empty(interfaces);
    }

    [Fact]
    public void MixedInterfaces_OnlyNuVatisOnesDetected() {
        var source = @"
using NuVatis.Attributes;

namespace AutoMapper
{
    public interface IMapper
    {
        object Map(object source);
    }
}

namespace MyApp.Mappers
{
    [NuVatisMapper]
    public interface ICustomerMapper
    {
        System.Threading.Tasks.Task<object> GetAll();
    }

    public interface INotAMapper
    {
        void DoSomething();
    }
}
";
        var compilation = CreateCompilation(source);
        var interfaces  = InterfaceAnalyzer.FindMapperInterfaces(compilation, CancellationToken.None);

        Assert.Single(interfaces);
        Assert.Equal("ICustomerMapper", interfaces[0].Name);
    }

    [Fact]
    public void InterfaceWithBothAttributeAndSqlAttr_IsDetected() {
        var source = @"
using NuVatis.Attributes;

namespace MyApp.Mappers
{
    [NuVatisMapper]
    public interface IReportMapper
    {
        [Select(""SELECT COUNT(*) FROM reports"")]
        System.Threading.Tasks.Task<long> CountAll();

        System.Threading.Tasks.Task<object> GetMonthlyStats(int year, int month);
    }
}
";
        var compilation = CreateCompilation(source);
        var interfaces  = InterfaceAnalyzer.FindMapperInterfaces(compilation, CancellationToken.None);

        Assert.Single(interfaces);
        Assert.Equal("IReportMapper", interfaces[0].Name);
        Assert.Equal(2, interfaces[0].Methods.Length);
    }

    [Fact]
    public void NoNuVatisInterfaces_ReturnsEmpty() {
        var source = @"
namespace SomeLibrary
{
    public interface IService
    {
        void Execute();
    }

    public interface IDataMapper
    {
        object Transform(object input);
    }
}
";
        var compilation = CreateCompilation(source);
        var interfaces  = InterfaceAnalyzer.FindMapperInterfaces(compilation, CancellationToken.None);

        Assert.Empty(interfaces);
    }

    private static Compilation CreateCompilation(string userSource) {
        var syntaxTrees = new[] {
            CSharpSyntaxTree.ParseText(NuVatisAttributesSources),
            CSharpSyntaxTree.ParseText(userSource)
        };

        var references = new[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location),
            MetadataReference.CreateFromFile(
                System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!,
                    "System.Runtime.dll"))
        };

        return CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
