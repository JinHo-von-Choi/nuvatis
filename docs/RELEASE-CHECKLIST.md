# NuVatis 릴리스 체크리스트

작성자: 최진호
작성일: 2026-02-28

릴리스 전 이 체크리스트를 순서대로 완료할 것.

---

## 1. 코드 검증

- [ ] 전체 테스트 통과
  ```bash
  dotnet test 2>&1 | tail -3
  # Expected: Failed: 0
  ```
- [ ] 빌드 경고 0개
  ```bash
  dotnet build --no-incremental -warnaserror 2>&1 | grep -c "warning"
  # Expected: 0
  ```
- [ ] PublicAPI.Shipped.txt 최신화 (RS0016/RS0017 경고 없음)
  ```bash
  dotnet build --no-incremental 2>&1 | grep "RS0016\|RS0017" | wc -l
  # Expected: 0
  ```
- [ ] CHANGELOG.md 해당 버전 항목 작성 완료

## 2. 패키지 품질

- [ ] XML doc 주석 빌드 확인
  ```bash
  ls src/NuVatis.Core/bin/Release/net8.0/NuVatis.Core.xml
  # Expected: 파일 존재
  ```
- [ ] SourceLink 동작 확인 (PDB에 소스 경로 포함)
- [ ] 로컬 패킹 테스트
  ```bash
  ./pack.sh
  ls nupkg/*.nupkg | wc -l
  # Expected: 11개
  ```
- [ ] 생성된 패키지 중 하나를 샘플 프로젝트에서 참조 테스트

## 3. 보안

- [ ] 시크릿 하드코딩 없음
  ```bash
  grep -rn "password\|apikey\|secret\|token" src/ --include="*.cs" \
    | grep -v "//\|/// " | grep -vi "Password\|ApiKey\|TokenType" | head -10
  ```
- [ ] NV004 DiagnosticSeverity.Error 유지 확인
  ```bash
  grep "NV004" src/NuVatis.Generators/Diagnostics/DiagnosticDescriptors.cs \
    | grep "Error"
  # Expected: 한 줄 출력
  ```

## 4. 버전 및 태그

- [ ] `Directory.Build.props`의 `<Version>` 업데이트
- [ ] `CHANGELOG.md` 버전 항목 날짜 확인
- [ ] git 커밋 정리 (`git log --oneline -10`)
- [ ] 미푸시 커밋 없음 확인
  ```bash
  git status
  git log origin/main..HEAD --oneline
  # Expected: 배포할 커밋들만 표시
  ```
- [ ] git tag 생성 및 push
  ```bash
  git tag v{VERSION}
  git push origin v{VERSION}
  ```

## 5. 배포

- [ ] GitHub Actions publish.yml 트리거 확인 (태그 push 후 자동 실행)
- [ ] NuGet.org 배포 확인 (11개 패키지)
  - NuVatis.Core, NuVatis.Generators
  - NuVatis.Sqlite, NuVatis.MySql, NuVatis.PostgreSql, NuVatis.SqlServer
  - NuVatis.Extensions.DependencyInjection, NuVatis.Extensions.EntityFrameworkCore
  - NuVatis.Extensions.OpenTelemetry, NuVatis.Extensions.Aspire
  - NuVatis.Testing
- [ ] GitHub Release 자동 생성 확인 (릴리스 노트 포함)
- [ ] 샘플 프로젝트를 NuGet.org 배포 버전으로 테스트
