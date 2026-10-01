namespace HelperManager.Modules.QueryStore.Models;

/// <summary>
/// XML로 추출된 쿼리 모음 파일에서 읽어온 쿼리 하나를 표현합니다.
/// 예) &lt;sql id="HIS.MS.CS.PH.CO.InsertClosingDegree"&gt;...&lt;/sql&gt;
/// </summary>
/// <param name="Id">쿼리 식별자. 점(.)으로 구분된 계층 구조를 가지며 트리 폴더 구조의 기준이 된다.</param>
/// <param name="Content">
/// &lt;sql&gt; ~ &lt;/sql&gt; 태그를 포함한 원본 텍스트 그대로. (주석 헤더 + SQL 본문 + 닫는 태그)
/// 화면에 보여줄 때도, 검색(쿼리 내용 검색)에도 이 원본 텍스트를 그대로 사용한다.
/// </param>
public sealed record QueryDefinition(string Id, string Content);
