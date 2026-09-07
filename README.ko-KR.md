# Il2CppDumper

[![Build status](https://ci.appveyor.com/api/projects/status/anhqw33vcpmp8ofa?svg=true)](https://ci.appveyor.com/project/Perfare/il2cppdumper/branch/master/artifacts)

[English](README.md) | [简体中文](README.zh-CN.md)

Unity IL2CPP 리버스 엔지니어링 도구

## 주요 기능

* 원본 메서드 본문을 제외한 DLL 메타데이터를 복원하여 `MonoBehaviour` 및 `MonoScript` 추출에 활용할 수 있습니다.
* ELF, ELF64, Mach-O, PE, NSO, WASM 형식을 지원합니다.
* 기존 Unity 5.3 이상 버전과 아래에 명시된 Unity 6 메타데이터 레이아웃을 지원합니다.
* 기존 메타데이터 16–31 파서를 유지하면서 35, 38, 39, 104–108, 110 및 106.1 바이너리 변형을 추가로 지원합니다. 가변 폭 인덱스, 열거형 타입, 인코딩된 특성 생성자, 재배치된 제네릭/RGCTX 테이블, 계산으로 결정되는 메타데이터 토큰을 복원합니다.
* 타입·멤버 특성뿐 아니라 메타데이터에 존재하는 어셈블리 사용자 지정 특성(v21+), 반환값 사용자 지정 특성(v31+), 모듈 사용자 지정 특성(v38+)도 복원합니다.
* 내장된 필드 RVA 초기화 바이트, 내부 구조를 알 수 없는 값 타입 저장 영역의 크기, 선언된 패킹, 명시적 레이아웃 값 타입의 필드 오프셋을 DummyDll에 복원합니다. 네이티브 분석 스크립트는 발견된 필드 RVA 참조에도 이름을 붙입니다.
* IL2CPP 파일 분석을 돕는 IDA, Ghidra, Binary Ninja 스크립트를 생성합니다.
* 구조체 정보가 포함된 헤더 파일을 생성합니다.
* 보호를 우회하기 위해 Android 메모리에서 덤프한 `libil2cpp.so` 파일을 지원합니다.
* 단순한 PE 보호의 우회를 지원합니다.

### 버전 지원 및 검증 범위

메타데이터 24.4(바이너리 24.5), 31, 39, 106, 108의 Windows PE 샘플로 전체 처리 과정을 검증했습니다. 다른 버전 분기는 레이아웃과 경계 사례를 검사했지만, 실제 게임을 대상으로 한 전체 검증이 완료된 것은 아닙니다. 특히 110의 토큰 재구성은 합성 데이터로만 검증했습니다. 알 수 없는 최신 버전은 이전 레이아웃으로 해석하지 않고 거부합니다.

106/107 버전에는 두 가지 바이너리 레이아웃이 사용될 수 있습니다. 특성 생성자 태그를 통해 106과 106.1을 자동으로 구분합니다. 해당 태그가 없다면 `config.json`의 `ForceIl2CppVersion`과 `ForceVersion`을 적절한 바이너리 레이아웃에 맞게 설정하세요.

네이티브 `.h` 생성은 기존 구버전 레이아웃 외에도 35, 38, 39, 104, 105, 106, 106.1, 107, 108, 110을 지원합니다. 핵심 클래스·메서드·타입 레이아웃은 32비트 및 64비트 Windows ABI 각각에 대해 Unity 공식 배포본의 헤더와 비교했으며, 디버그 및 코드 커버리지 변형도 포함합니다. 최신 버전의 메모리 덤프와 PE 이외의 바이너리에는 아직 같은 수준의 전체 처리 검증을 수행하지 않았습니다.

## 사용법

`Il2CppDumper.exe`를 실행하여 IL2CPP 실행 파일과 `global-metadata.dat` 파일을 선택한 다음, 안내에 따라 필요한 정보를 입력하세요.

프로그램이 현재 작업 디렉터리에 출력 파일을 생성합니다.

### 명령줄

```
Il2CppDumper.exe <executable-file> <global-metadata> <output-directory>
```

명령을 실행하기 전에 출력 디렉터리를 만들어 두세요. 선택 옵션은 파일 경로 앞이나 뒤에 지정할 수 있습니다.

* `--strings-only`: `stringliteral.json`만 출력하고 `dump.cs`, `script.json`, 헤더, DummyDll 생성은 생략합니다. 기존의 관련 없는 출력 파일은 그대로 둡니다. 바이너리/메타데이터 초기화는 여전히 필요합니다. 출력에는 발견된 문자열 참조와 해당 RVA가 포함되며, 모든 현지화 에셋이나 사용되지 않은 메타데이터 문자열을 담는 것은 아닙니다. v16에서는 주소 정보가 포함된 문자열 추출을 지원하지 않습니다.
* `--restore-explicit-interfaces`: 모호하지 않은 명시적 인터페이스 매핑을 DummyDll에 추가합니다. 이 기능은 명시적으로 활성화해야 하며, 보수적으로 복원합니다. 타입이 직접 구현한 비제네릭 인터페이스여야 하고, 메서드 이름·플래그·타입의 소속 범위를 포함한 시그니처가 일치해야 합니다. 제네릭 메서드/인터페이스, 상속을 통해서만 구현되는 인터페이스, 여러 해석이 가능한 매칭은 변경하지 않습니다.

### Ghidra 기본 적용법

Il2CppDumper에 입력했던 네이티브 바이너리와 해당 파일에서 생성한 `il2cpp.h`, `script.json`을 사용합니다. Windows에서는 `GameAssembly.dll` 또는 해당 `*Assembly.dll`, Android에서는 `libil2cpp.so`를 가져오세요. `DummyDll` 안의 DLL은 네이티브 분석 대상으로 가져오지 않습니다.

1. Ghidra에서 `File → New Project → Non-Shared Project`로 프로젝트를 만들고, `File → Import File`로 네이티브 바이너리를 가져옵니다. CodeBrowser로 연 뒤 처음 나타나는 Auto Analysis 질문에서는 `No`를 선택합니다.

2. `il2cpp.h`와 같은 폴더에 `il2cpp_header_to_ghidra.py`를 둡니다. Ghidra 밖에서 해당 폴더를 현재 작업 디렉터리로 열고 Python 3으로 변환기를 실행합니다.

   ```text
   python -X utf8 il2cpp_header_to_ghidra.py
   ```

   `il2cpp_ghidra.h`가 생성됩니다. 최신 헤더는 포인터 폭을 자동으로 인식합니다. 아키텍처 표시가 없는 구형 32비트 헤더에는 `--bits 32`를 추가하세요.

3. CodeBrowser에서 `File → Parse C Source...`를 엽니다. 대상 아키텍처에 맞는 파싱 프로필을 복사하고, 소스 파일 목록을 `il2cpp_ghidra.h`로 교체한 뒤 관련 없는 include 경로와 파싱 옵션을 제거합니다. `Parse to Program`을 눌러 현재 프로그램에 자료형을 등록합니다.

4. `Window → Script Manager`를 엽니다. 스크립트 디렉터리 관리 메뉴에서 Il2CppDumper의 Ghidra 스크립트 폴더를 추가하고 목록을 새로 고칩니다. PyGhidra를 사용한다면 Ghidra를 [PyGhidra 모드](https://github.com/NationalSecurityAgency/ghidra/blob/master/Ghidra/Features/PyGhidra/src/main/py/README.md)로 실행하고, 로컬 `ghidra_with_struct.py` 복사본의 머리말 주석에 `#@runtime PyGhidra`를 추가하세요. Jython 사용자는 동봉된 스크립트를 Jython으로 실행하면 됩니다.

5. `ghidra_with_struct.py`를 실행하고 파일 선택창에서 `script.json`을 선택합니다. 3단계에서 등록한 자료형을 사용하여 이름, 함수 시그니처, 메타데이터 자료형을 적용합니다. `ghidra.py`를 따로 실행할 필요는 없습니다.

6. `Analysis → Auto Analyze...`를 실행합니다. 대상 바이너리의 기본 분석 옵션을 기준으로 시작하되, 첫 분석에서는 `Decompiler Parameter ID`를 해제해 둡니다. 분석이 끝나면 함수를 선택하고 `Window → Decompiler`에서 네이티브 의사 코드를 확인합니다.

이미 Auto Analysis를 실행했다면 헤더 변환 단계부터 이어서 진행하세요. 헤더의 자료형 등록은 `ghidra_with_struct.py` 실행보다 먼저 해야 합니다.

### 자동 릴리즈

`master`에 푸시하면 해당 커밋 해시의 앞 12자리 16진수를 태그로 사용하는 GitHub 릴리즈가 게시됩니다. 전체 커밋 SHA는 릴리즈 노트에 기록됩니다. Windows x64 및 x86 ZIP에는 설정 파일, 분석 스크립트, 문서가 포함됩니다.

* `net8.0-win-x64-self-contained` / `net8.0-win-x86-self-contained`(권장): .NET 런타임이 포함되어 있습니다. ZIP 전체를 압축 해제한 뒤 `Il2CppDumper.exe`를 실행하세요. .NET을 별도로 설치할 필요가 없습니다.
* `net8.0-win-{x64,x86}-framework-dependent`: 다운로드 용량이 더 작지만, 패키지와 같은 아키텍처의 .NET 8 런타임이 필요합니다. 지원이 종료된 .NET 6 대상은 더 이상 빌드하지 않습니다.

Windows PE 입력의 경우 32비트 바이너리에는 x86, 64비트 바이너리에는 x64 패키지를 선택하세요. 이렇게 해야 사용자 지정 PE 로더도 같은 아키텍처의 바이너리를 로드할 수 있습니다. x86과 x64 패키지는 서로 다른 디렉터리에 압축 해제하고, 런타임 DLL을 섞지 마세요.

`Release commit` 워크플로는 `master`에서 수동으로 실행할 수도 있습니다. 네 패키지 모두 빌드와 CLI 기본 실행 검사를 통과해야 릴리즈를 생성합니다. 업로드는 릴리즈가 초안 상태일 때 완료됩니다. 재실행하면 초안 릴리즈의 작업을 이어갈 수 있지만, 이미 게시된 릴리즈는 변경하지 않습니다. 워크플로는 `contents: write` 권한을 가진 저장소의 기본 `GITHUB_TOKEN`을 사용하므로 별도의 시크릿이 필요하지 않습니다.

### 출력 파일

#### DummyDll

복원한 모든 DLL 파일이 들어 있는 폴더입니다.

[dnSpy](https://github.com/0xd4d/dnSpy), [ILSpy](https://github.com/icsharpcode/ILSpy) 또는 다른 .NET 디컴파일러로 확인할 수 있습니다.

사용자 지정 특성은 대체 타입이나 메서드가 아니라 원래의 어셈블리, 모듈 또는 메서드 반환값에 연결됩니다. 따라서 어셈블리 메타데이터, 모듈 주석, 읽기 전용 반환값, 반환 튜플의 요소 이름 같은 정보를 보존합니다. 구버전 v21–27의 특성 인자는 기존과 마찬가지로 가능한 범위에서만 복원하며, 없는 메타데이터를 추정해서 채우지 않습니다.

내장된 필드 RVA 데이터는 고정 폭 기본 타입 필드와 크기를 복원할 수 있는 비제네릭 불투명 값 타입 저장 영역에 대해 복원합니다. 바이트 범위는 메타데이터의 기본값 섹션 경계를 기준으로 검사합니다. 지원하지 않는 저장 영역 레이아웃은 경고를 출력하고 메타데이터 오프셋 주석을 유지합니다. 임의의 정적 생성자 로직이나 런타임에 계산되는 값은 복원하지 않습니다. 원본 메서드 본문을 재현하는 기능이 아니며, 디컴파일러가 원래의 고수준 배열 선언을 재구성한다고 보장하지도 않습니다.

`DumpAttribute`를 활성화하면 `dump.cs`에도 `assembly:`, `module:`, `return:` 대상을 사용하여 이러한 특성을 출력합니다. 덤프는 여러 어셈블리를 하나로 합치므로 어셈블리/모듈 그룹에 원본 이미지 정보를 표시합니다. 이는 분석용 목록이며, 하나의 컴파일 가능한 C# 어셈블리가 아닙니다.

[UtinyRipper](https://github.com/mafaca/UtinyRipper), [UABE](https://7daystodie.com/forums/showthread.php?22675-Unity-Assets-Bundle-Extractor) 등에서 Unity의 `MonoBehaviour`와 `MonoScript`를 추출하는 데 사용할 수 있습니다.

#### ida.py

IDA용 스크립트입니다.

#### ida_with_struct.py

IDA용 스크립트로, il2cpp.h 파일을 읽어 IDA에 구조체 정보를 적용합니다.

Python 3에서는 `ida_py3.py` 또는 `ida_with_struct_py3.py`를 사용하세요. 후자는 [IDA 9 타입 API](https://python.docs.hex-rays.com/9.0/df/d81/namespaceida__typeinf.html)를 사용합니다. 두 스크립트 모두 모듈을 명시적으로 가져오며 파일 선택이나 가져오기가 취소되는 경우를 처리합니다. 바이너리 바이트를 패치하거나 인위적인 문자열 세그먼트를 만들지 않고 분석 주석만 추가합니다. 기존 Python 2 스크립트는 변경하지 않았습니다.

#### il2cpp.h

구조체 정보가 포함된 헤더 파일입니다.

최신 버전의 헤더는 입력 바이너리의 포인터 폭에 맞춰 생성됩니다. 플레이어가 해당 네이티브 런타임 설정으로 빌드되었다면 v104+ 헤더를 가져오기 전에 `IL2CPP_DEBUG`를 `1`로 설정하세요. 네이티브 코드 커버리지를 사용하는 v110 플레이어라면 `IL2CPP_CODE_COVERAGE`를 `1`로 설정하세요. 매크로를 정의하지 않으면 일반 레이아웃을 선택합니다. 이러한 빌드 설정은 메타데이터에서 자동으로 감지하지 않으며, 단순히 디버거를 연결하는 것과도 다릅니다. 일치하는 ABI로 가져오세요. `il2cpp_header_to_ghidra.py`는 최신 헤더의 포인터 폭을 감지합니다. 포인터 폭 표시가 없는 구형 32비트 헤더를 변환할 때는 `--bits 32`를 사용하세요.

헤더는 알 수 없는 RGCTX 슬롯을 유지하여 뒤따르는 항목의 위치가 밀리지 않도록 하며, 1바이트 패킹이 적용된 불투명 초기화 저장 영역의 바이트 크기도 보존합니다. 생성된 게임별 구조체는 분석을 돕는 자료이며, Unity 원본 헤더나 컴파일 가능한 게임 원본 소스를 대체하지 않습니다.

#### ghidra.py

Ghidra용 스크립트입니다.

`ghidra.py`와 `ghidra_with_struct.py`는 Jython 또는 Python 3/PyGhidra에서 유니코드 텍스트를 처리합니다. 구조체 정보를 사용하는 스크립트는 먼저 호환되는 헤더를 가져와야 합니다(`il2cpp_header_to_ghidra.py`는 Python 3 변환기입니다). Script Manager의 PyGhidra 실행기를 사용하려면 로컬 스크립트 복사본에 `#@runtime PyGhidra`를 추가하고 Ghidra를 [PyGhidra 모드](https://github.com/NationalSecurityAgency/ghidra/blob/master/Ghidra/Features/PyGhidra/src/main/py/README.md)로 실행하세요. 기존 Jython 사용자의 런타임 선택은 자동으로 변경하지 않습니다. WASM/플러그인 전용 스크립트는 변경하지 않았습니다.

#### Il2CppBinaryNinja

Binary Ninja용입니다.

#### ghidra_wasm.py

[ghidra-wasm-plugin](https://github.com/nneonneo/ghidra-wasm-plugin)과 함께 사용하는 Ghidra용 스크립트입니다.

#### script.json

ida.py, ghidra.py, Il2CppBinaryNinja에서 사용하는 파일입니다.

#### stringliteral.json

모든 stringLiteral 정보가 포함된 파일입니다.

### 설정

모든 설정 옵션은 `config.json`에 있습니다.

사용 가능한 옵션은 다음과 같습니다.

* `DumpMethod`, `DumpField`, `DumpProperty`, `DumpAttribute`, `DumpFieldOffset`, `DumpMethodOffset`, `DumpTypeDefIndex`

  * 해당 정보를 dump.cs에 출력할지 지정합니다.

* `GenerateDummyDll`, `GenerateStruct`

  * DummyDll과 구조체 헤더를 각각 생성할지 지정합니다.

* `StringsOnly`(기본값 `false`)

  * `--strings-only`와 같으며, 다른 출력 설정보다 우선합니다.

* `RestoreExplicitInterfaces`(기본값 `false`)

  * `--restore-explicit-interfaces`와 같으며, DummyDll 생성에만 영향을 줍니다.

* `DummyDllAddToken`

  * DummyDll에 토큰을 추가할지 지정합니다.

* `RequireAnyKey`

  * 작업이 끝난 뒤 아무 키나 눌러야 종료하도록 할지 지정합니다.

* `ForceIl2CppVersion`, `ForceVersion`

  * `ForceIl2CppVersion`이 `true`이면 `ForceVersion`에 지정한 버전 번호로 IL2CPP 바이너리 파서를 선택합니다. 메타데이터 파서의 선택에는 영향을 주지 않습니다. 일부 구버전 IL2CPP에서 유용할 수 있습니다. 예를 들어 IL2CPP v20(Android) 바이너리를 정상적으로 처리하려면 v16 파서를 사용해야 하는 경우가 있습니다.

* `ForceDump`

  * 파일을 메모리 덤프 파일로 강제 처리합니다.

* `NoRedirectedPointer`

  * 덤프 파일의 포인터를 리디렉션되지 않은 포인터로 취급합니다. 일부 기기에서 덤프한 파일은 이 옵션을 `true`로 설정해야 합니다.

## 자주 발생하는 오류

#### `ERROR: Metadata file supplied is not valid metadata file.`

올바른 파일을 선택했는지 확인하세요. 게임에 따라 콘텐츠 보호 등의 목적으로 이 파일을 난독화하기도 합니다. 이러한 파일의 난독화 해제는 이 프로그램의 범위 밖이므로, 난독화 해제를 요청하는 이슈는 등록하지 마세요.

입력 파일이 `libil2cpp.so`이고 루팅된 Android 기기가 있다면 별도 프로젝트인 [Zygisk-Il2CppDumper](https://github.com/Perfare/Zygisk-Il2CppDumper)를 사용해 볼 수 있습니다. 이 보호를 우회할 수 있습니다.

#### `ERROR: Can't use auto mode to process file, try manual mode.`

PC 플랫폼에서 선택해야 하는 실행 파일은 `GameAssembly.dll` 또는 `*Assembly.dll`입니다.

새 이슈를 열어 파일을 첨부하면 문제 해결을 위한 검토를 요청할 수 있습니다.

#### `ERROR: This file may be protected.`

Il2CppDumper가 실행 파일의 보호를 감지했습니다. `GameGuardian`으로 게임 메모리에서 `libil2cpp.so`를 덤프한 뒤 Il2CppDumper로 로드하고 안내에 따라 진행하면 대부분의 보호를 우회할 수 있습니다.

루팅된 Android 기기가 있다면 별도 프로젝트인 [Zygisk-Il2CppDumper](https://github.com/Perfare/Zygisk-Il2CppDumper)를 사용해 볼 수 있습니다. 거의 모든 보호를 우회할 수 있습니다.

## 기여 및 참고 프로젝트

- Jumboperson - [Il2CppDumper](https://github.com/Jumboperson/Il2CppDumper)
- [c01ns](https://github.com/c01ns/Il2CppDumper), [vmpprotect](https://github.com/vmpprotect/Il2CppDumper), [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL) - 최신 메타데이터 레이아웃 참고 자료
- [Il2CppInspectorRedux](https://github.com/LukeFZ/Il2CppInspectorRedux) - 메타데이터 버전/레이아웃 교차 검증에 참고했으며, 런타임 의존성이 아닙니다.
- [jules-noelaudoux](https://github.com/jules-noelaudoux/Il2CppDumper), [MSerperior](https://github.com/MSerperior/Il2CppDumper) - IDA/Python 3 호환성 참고 자료
- [liu-shuoye](https://github.com/liu-shuoye/Il2CppDumper) - 명시적 인터페이스 재구성 방식 참고 자료
- [b1naryTR](https://github.com/b1naryTR/Il2CppDumper) - 경량 문자열 추출 방식 참고 자료
