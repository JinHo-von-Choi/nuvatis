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

## 테스트 트레이트 규약

CI는 트레이트로 테스트를 분류해 단계별로 실행한다. **DB별 E2E는 `Category`와 `Provider`를 함께 태그해야 한다** — `Provider`가 없으면 CI 필터가 0건을 매칭해 단계가 green으로 통과하지만 아무 테스트도 실행하지 않는다.

| Category | 대상 | 필요한 환경 |
|----------|------|-------------|
| (없음) | 순수 단위 테스트 | 없음 |
| `SqliteE2E` | SQLite 인메모리 E2E — CI 유닛 잡에서 실행 | 없음 |
| `E2E` | 외부 DB 서비스 컨테이너 E2E | DB |
| `Testcontainers` | Testcontainers 기반 E2E | Docker |
| `Integration` | QueryBuilder 통합 테스트 | Docker |

`Provider` 값: `PostgreSql`, `MySql`, `SqlServer`.

### CI 실행 구성

| 단계 | 필터 | 최소 통과 개수 |
|------|------|----------------|
| Unit (Core) | `Category!=E2E&Category!=Testcontainers` | 415 |
| Unit (Generators) | (필터 없음) | 165 |
| Unit (QueryBuilder) | `Category!=Integration` | 72 |
| E2E PostgreSQL | `Category=E2E&Provider=PostgreSql` | 7 |
| E2E MySQL | `Category=Testcontainers&Provider=MySql` | 2 |
| E2E SQL Server | `Category=Testcontainers&Provider=SqlServer` | 5 |

각 단계는 최소 통과 개수를 검증한다. 0건 매칭이 재발하면 잡이 즉시 실패한다. 새 테스트를 추가하거나 제거할 때 임계값을 갱신해야 한다.

**임계값은 Release 빌드의 실측값에서 잡는다.** 2026-10-04 v2.8.0 릴리스 시 ubuntu 전 TFM에서
동일하게 관측된 값이다.

| 스위트 | 관측 통과 개수 | 임계값 |
|--------|----------------|--------|
| Generators | 173 | 165 |
| Core (`!E2E & !Testcontainers`) | 422 | 415 |
| QueryBuilder (`!Integration`) | 76 | 72 |
| QueryBuilder.Tools | 15 | 14 |

> **Debug 빌드에서는 테스트가 2배로 집계된다.** `dotnet test -c Debug` 는 Generators 346 /
> QueryBuilder.Tools 30 을 보고하지만 Release 는 173 / 15 다. TRX 에서 고유 테스트 이름
> 173개가 **전부 정확히 2회씩** 기록되어 있어 실행 중복이며, `dotnet clean` 후에도 재현된다.
> 임계값을 Debug 수치로 잡으면 검증이 통상적 2분의 1만 요구하게 되어 무의미해지므로
> **반드시 Release 기준선을 쓴다.** 중복의 근본 원인은 아직 특정하지 못했다 — xunit 러너
> 또는 테스트 호스트 레벨의 문제로 보인다.

> 두 검증 스텝(`Verify test counts`, `Verify E2E test counts`)은 bash 문법(`declare -A`)을
> 쓰므로 `shell: bash` 를 명시해야 한다. 미지정 시 windows-latest 의 기본 셸인 pwsh 에서
> `declare` 를 알 수 없어 **테스트는 전부 통과한 상태에서 잡만 red** 가 된다(실측 2026-10-04).

> **멀티타겟 프로젝트는 6개 TFM 을 전부 실행하고 같은 TRX 를 6번 덮어쓴다.**
> 빌드-유닛 스텝에 `-f`/`--framework` 가 없기 때문이다. ubuntu 는 덮어쓰기에 성공하지만
> windows 는 테스트 호스트가 파일 핸들을 놓지 않아
> `Failed to write the results file ... being used by another process` 가 발생하고 TRX 의
> `passed` 속성을 잃는다. 그 결과 **422개가 전부 통과한 core 스위트가 `passed=0` 으로
> 보고되어 잡이 실패했다**(실측 2026-10-04 — 3회 연속 동일 증상).
>
> `--framework ${{ matrix.tfm }}` 으로 매트릭스에서 TFM 을 고정해 해결을 시도했으나
> **net6/7/9/10/11 잡이 오히려 깨졌다**(`No test is available in .../net6.0/....dll`,
> `MSB4181: VSTestTask returned false`). 해당 러너에서 TFM 고정이 테스트 호스트 부재로
> 이어지는 문제이며, 정확히 무엇이 원인인지 특정하지 못했다. 되돌렸다.
>
 >
> **`grep` 에는 반드시 `|| true` 를 붙일 것.** GitHub Actions 의 bash 는 `-e` 로
> 돌아서, 검증 스텝의 `PASSED=$(grep ... )` 처럼 어긋나면 exit 1 인 명령이 루프 중간에
> 스크립트를 죽인다. 트리플이 죽은 채 어떤 키도 보고하지 않아 **"테스트는 전부 통과했는데
> 잡만 실패"** 인 것처럼 보인다. TRX 루트의 속성명도 `skipped` 가 아니라 `notExecuted` 다.
> (실측 2026-10-04 — E2E 검증 스텝은 이 결함 때문에 한 번도 정상 통과한 적이 없었다.
>  PostgreSQL 7 / MySQL 2 / SQL Server 5가 모두 통과했는데 잡이 실패했다.)

> **E2E 잡도 TFM 을 고정해야 한다.** 같은 이유다. E2E 잡에는 매트릭스가 없어 TFM 이
> 고정되지 않으면 6개 TFM 이 전부 실행되는데, `net6.0`/`net7.0` 어셈블리에는
> `Category=E2E&Provider=*` 테스트가 하나도 없어 `No test matches` 가 되고 0건 매칭을
> 실패로 보는 `dotnet test` 가 exit 1 을 낸다(실측 2026-10-04 — net8~net11 은 6~7개
> 통과, net6/7 은 0건). E2E 잡 3개 스텝을 `--framework net8.0` 으로 고정했다.
>
> **현재 해법**: `Verify test counts` 를 ubuntu 잡에서만 실행한다(`if: matrix.os ==
> 'ubuntu-latest'`). 검증의 목적 — 0건 매칭의 green 통과 차단 — 은 OS 와 무관하므로
> ubuntu 만으로 충족된다. windows 잡은 사유를 담은 경고를 남긴다.
>
> **미해결**: windows 에서도 최소 개수 검증을 하려면 (1) 매트릭스에 TFM 을 넣되
> 해당 러너에 테스트 호스트가 있는 조합으로만, 또는 (2) TRX 대신 스텝 출력을 파싱하는
> 방식으로 바꿔야 한다. 어느 쪽이든 추가 검증이 필요하다.

> **windows 러너는 ubuntu에서 드러나지 않는 결함이 숨는다.** 위 두 건 모두 ubuntu 6개 잡은
> 정상이고 windows/net8 만 실패했다. 매트릭스에 windows 가 없으면 검증 자체가 통과해 버린다.

### 외부 DB 연결 문자열

환경변수로 주입한다. 하드코딩된 포트/계정은 CI와 로컬이 어긋난다.

| 환경변수 | 사용처 |
|----------|--------|
| `NUVATIS_TEST_PG_CONNECTION` | `PostgreSqlE2ETests` |
| `TC_PG_IMAGE` | Testcontainers PostgreSQL 이미지 (기본 `postgres:16-alpine`) |
| `TC_MYSQL_IMAGE` | Testcontainers MySQL 이미지 (기본 `mysql:8.0`) |
| `TC_MSSQL_IMAGE` | Testcontainers SQL Server 이미지 |

> Testcontainers 계열은 `IAsyncLifetime`을 구현한다. `xunit.runner.visualstudio`가 3.x 미만이면 이 클래스가 VSTest에 등록되지 않아 조용히 미실행된다(실측). 러너 버전 상향 시 반드시 `Category=Testcontainers` 필터가 0이 아닌지 확인할 것.

## 생성 코드 회귀 방지

Source Generator를 리팩터링할 때 **생성 출력이 1글자도 달라지면 안 된다.** 공백이나 로컬 변수명만 바뀌어도 동작이 달라질 수 있다.

`GeneratorGoldenSnapshotTests`가 4개 코퍼스(속성 매퍼 / `if`·`where`·`set`·`choose` / 중첩 `foreach` / `${}` 치환)의 전체 생성 출력을 SHA-256으로 고정한다. 기존 `GeneratorIntegrationTests`는 `Assert.Contains(부분 문자열)` 기반이라 이런 변화를 잡지 못한다.

- 임계값 해시는 테스트 하단에 상수로 고정돼 있다.
- **의도한 출력 변경일 때만** `REGRESSION_BASELINE=1`로 실행해 새 해시를 얻고 상수를 갱신한다.
- 함께 고정되는 내용: `BuildSql_` 정적 방출 경로, `__sb_`/`__params_`/`__idx_` 로컬 변수, `${}`의 SqlIdentifier 런타임 가드, `foreach` 루프, `NuVatisMapperRegistry` / `NuVatisTypeMappers` 생성물.

> 방출 경로가 실제로 태워지는지도 별도 테스트(`GeneratedOutput_ActuallyCoversEmitterPaths`)로 검증한다. XML이 깨져 생성기가 아무것도 내지 않아도 해시는 통과하기 때문이다 — `CS8785` 진단이 포함되지 않았음을 함께 확인한다.

## 테스트 전략 가이드

| 계층 | 테스트 방식 | 도구 |
|------|-----------|------|
| Mapper XML 파싱 | Unit Test | XmlMapperParser 직접 호출 |
| Source Generator | Unit Test | Roslyn Compilation mock |
| Service Logic | Unit Test | InMemorySqlSession + QueryCapture |
| DB CRUD | E2E Test | 실제 DB + 트랜잭션 격리 |
| 성능 | Benchmark | BenchmarkDotNet |
