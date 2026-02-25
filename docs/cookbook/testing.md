# Testing Cookbook

## InMemorySqlSession

DB 연결 없이 비즈니스 로직을 테스트한다.

```csharp
[Fact]
public void GetById_Returns_User_When_Exists() {
    var session = new InMemorySqlSession();
    var expectedUser = new User { Id = 1, UserName = "jinho" };

    session.Setup("UserMapper.GetById", expectedUser);

    var result = session.SelectOne<User>("UserMapper.GetById", new { Id = 1 });

    Assert.NotNull(result);
    Assert.Equal("jinho", result.UserName);
}
```

## QueryCapture - 쿼리 호출 검증

```csharp
[Fact]
public void Insert_Invokes_Correct_Statement() {
    var session = new InMemorySqlSession();
    session.Setup("UserMapper.Insert", 1);

    session.Insert("UserMapper.Insert", new User { UserName = "test" });

    Assert.True(QueryCapture.HasQuery(session, "UserMapper.Insert"));
    Assert.Equal(1, QueryCapture.QueryCount(session, "UserMapper.Insert"));
}
```

## Service Layer 테스트

```csharp
public class UserServiceTests {
    [Fact]
    public async Task SearchUsers_Returns_Filtered_Results() {
        var session = new InMemorySqlSession();
        var users = new List<User> {
            new() { Id = 1, UserName = "admin" },
            new() { Id = 2, UserName = "user1" }
        };
        session.Setup("UserMapper.Search", users);

        var service = new UserService(session);
        var result = await service.SearchUsers("admin");

        Assert.NotEmpty(result);
    }
}
```

## E2E 테스트 (실제 DB)

실제 DB를 사용하는 E2E 테스트는 트랜잭션으로 격리한다.

```csharp
public class UserMapperE2ETests : IDisposable {
    private readonly ISqlSession _session;

    public UserMapperE2ETests() {
        var factory = TestFixture.CreateFactory();
        _session = factory.OpenSession();
    }

    [Fact]
    public void Insert_And_Select_RoundTrip() {
        var mapper = _session.GetMapper<IUserMapper>();

        mapper.Insert(new User { UserName = "e2e_test", Email = "test@test.com" });

        var user = mapper.GetById(1);
        Assert.NotNull(user);
        Assert.Equal("e2e_test", user.UserName);
    }

    public void Dispose() {
        _session.Rollback();
        _session.Dispose();
    }
}
```

Rollback으로 테스트 데이터를 자동 정리한다.

## 테스트 전략 가이드

| 계층 | 테스트 방식 | 도구 |
|------|-----------|------|
| Mapper XML 파싱 | Unit Test | XmlMapperParser 직접 호출 |
| Source Generator | Unit Test | Roslyn Compilation mock |
| Service Logic | Unit Test | InMemorySqlSession + QueryCapture |
| DB CRUD | E2E Test | 실제 DB + 트랜잭션 격리 |
| 성능 | Benchmark | BenchmarkDotNet |
