using System.Data.Common;
using NuVatis.Mapping;
using NuVatis.Mapping.TypeHandlers;
using Xunit;

namespace NuVatis.Tests;

/**
 * TypeHandlerRegistry 단위 테스트.
 *
 * @author 최진호
 * @date   2026-02-26
 */
public class TypeHandlerRegistryTests {

    private sealed class StubHandler : ITypeHandler {
        public Type TargetType => typeof(int);
        public object? GetValue(DbDataReader reader, int ordinal) => null;
        public void SetParameter(DbParameter parameter, object? value) { }
    }

    [Fact]
    public void Register_ByType_And_Get() {
        var registry = new TypeHandlerRegistry();
        var handler  = new StubHandler();
        registry.Register(handler);

        var result = registry.Get(typeof(int));
        Assert.Same(handler, result);
    }

    [Fact]
    public void Get_ByType_Unknown_Returns_Null() {
        var registry = new TypeHandlerRegistry();
        Assert.Null(registry.Get(typeof(string)));
    }

    [Fact]
    public void Register_ByName_And_Get() {
        var registry = new TypeHandlerRegistry();
        var handler  = new StubHandler();
        registry.Register("MyHandler", handler);

        Assert.Same(handler, registry.Get("MyHandler"));
        Assert.Same(handler, registry.Get(typeof(int)));
    }

    [Fact]
    public void Get_ByName_CaseInsensitive() {
        var registry = new TypeHandlerRegistry();
        var handler  = new StubHandler();
        registry.Register("MyHandler", handler);

        Assert.Same(handler, registry.Get("myhandler"));
        Assert.Same(handler, registry.Get("MYHANDLER"));
    }

    [Fact]
    public void Get_ByName_Unknown_Returns_Null() {
        var registry = new TypeHandlerRegistry();
        Assert.Null(registry.Get("nonexistent"));
    }

    [Fact]
    public void Register_Overwrite_ByType() {
        var registry = new TypeHandlerRegistry();
        var handler1 = new StubHandler();
        var handler2 = new StubHandler();
        registry.Register(handler1);
        registry.Register(handler2);
        Assert.Same(handler2, registry.Get(typeof(int)));
    }

    [Fact]
    public void BuiltIn_DateOnly_Handler() {
        var registry = new TypeHandlerRegistry();
        var handler  = new DateOnlyTypeHandler();
        registry.Register(handler);
        Assert.Same(handler, registry.Get(typeof(DateOnly)));
    }

    [Fact]
    public void BuiltIn_TimeOnly_Handler() {
        var registry = new TypeHandlerRegistry();
        var handler  = new TimeOnlyTypeHandler();
        registry.Register(handler);
        Assert.Same(handler, registry.Get(typeof(TimeOnly)));
    }
}
