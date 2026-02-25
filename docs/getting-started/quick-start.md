# Quick Start

5분 안에 NuVatis로 첫 쿼리를 실행하는 가이드.

## 1. Mapper Interface 정의

```csharp
using NuVatis.Attributes;

[NuVatisMapper]
public interface IUserMapper {
    User? GetById(int id);
    Task<User?> GetByIdAsync(int id, CancellationToken ct = default);
    IList<User> Search(UserSearchParam param);
    int Insert(User user);
    int Update(User user);
    int Delete(int id);
}
```

`[NuVatisMapper]` 어트리뷰트는 Source Generator가 이 인터페이스를 스캔 대상으로 인식하게 한다.

## 2. XML Mapper 작성

`Mappers/UserMapper.xml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<mapper namespace="MyApp.Mappers.IUserMapper">

  <resultMap id="UserResult" type="User">
    <id column="id" property="Id" />
    <result column="user_name" property="UserName" />
    <result column="email" property="Email" />
    <result column="created_at" property="CreatedAt" />
  </resultMap>

  <select id="GetById" resultMap="UserResult">
    SELECT id, user_name, email, created_at
    FROM users WHERE id = #{Id}
  </select>

  <select id="Search" resultMap="UserResult">
    SELECT id, user_name, email, created_at FROM users
    <where>
      <if test="UserName != null">
        AND user_name LIKE #{UserName}
      </if>
      <if test="Email != null">
        AND email = #{Email}
      </if>
    </where>
    ORDER BY created_at DESC
  </select>

  <insert id="Insert">
    INSERT INTO users (user_name, email)
    VALUES (#{UserName}, #{Email})
  </insert>

  <update id="Update">
    UPDATE users
    <set>
      <if test="UserName != null">user_name = #{UserName},</if>
      <if test="Email != null">email = #{Email},</if>
    </set>
    WHERE id = #{Id}
  </update>

  <delete id="Delete">
    DELETE FROM users WHERE id = #{Id}
  </delete>

</mapper>
```

namespace는 인터페이스의 정규화된 이름과 정확히 일치해야 한다.

## 3. DI 등록 (ASP.NET Core)

```csharp
builder.Services.AddNuVatis(options => {
    options.ConnectionString = builder.Configuration.GetConnectionString("Default");
    options.Provider         = new PostgreSqlProvider();
    options.RegisterMappers(NuVatisMapperRegistry.RegisterAll);
    options.RegisterAttributeStatements(NuVatisMapperRegistry.RegisterAttributeStatements);
});
```

`NuVatisMapperRegistry`는 Source Generator가 자동 생성한다.

## 4. 서비스에서 사용

```csharp
public class UserService {
    private readonly IUserMapper _mapper;

    public UserService(IUserMapper mapper) {
        _mapper = mapper;
    }

    public async Task<User?> GetUser(int id) {
        return await _mapper.GetByIdAsync(id);
    }

    public async Task<IList<User>> SearchUsers(string? name) {
        return _mapper.Search(new UserSearchParam { UserName = name });
    }
}
```

## 5. 빌드 및 실행

```bash
dotnet build   # Source Generator가 IUserMapper 구현체를 자동 생성
dotnet run
```

빌드 시 `obj/` 하위에 `IUserMapperImpl.g.cs`와 `NuVatisMapperRegistry.g.cs`가 생성되는 것을 확인할 수 있다.
