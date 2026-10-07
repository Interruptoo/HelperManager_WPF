namespace HelperManager.Modules.ComnCode.Models;

/// <summary>
/// ComnCdInfo.json / ComnCdDetail.json 에서 읽어온 레코드 하나입니다.
/// 원본 필드는 유연하게(대소문자 구분 없이) key-value 로 보관해서 조인 키 추출에 쓰고,
/// 실제 두 파일에서 확인된 필드들은 화면에 고정 컬럼으로 빠르게 보여줄 수 있도록
/// 이름 있는 속성으로도 노출한다. (DataTable 기반 동적 컬럼 생성은 매 필터링마다 테이블을
/// 다시 만들고 컬럼을 재생성해야 해서 느렸기 때문에, 알려진 스키마를 고정 속성으로 바꿨다.)
/// </summary>
public sealed class ComnCodeRecord
{
    /// <summary>원본 JSON 객체의 모든 필드. 키는 대소문자를 구분하지 않고 찾을 수 있다.</summary>
    public required IReadOnlyDictionary<string, string?> Fields { get; init; }

    // ----- 조인 키 (ComnCdInfo / ComnCdDetail 공통) -----
    public string? TableName => GetField("TABLE_NAME");
    public string? HspTpCd => GetField("HSP_TP_CD");
    public string? ComnGrpCd => GetField("COMN_GRP_CD");

    // ----- ComnCdInfo 전용 -----
    public string? ComnGrpCdNm => GetField("COMN_GRP_CD_NM");
    public string? ComnGrpCdExpl => GetField("COMN_GRP_CD_EXPL");

    // ----- ComnCdDetail 전용 -----
    public string? ComnCd => GetField("COMN_CD");
    public string? ComnCdNm => GetField("COMN_CD_NM");
    public string? ComnCdExpl => GetField("COMN_CD_EXPL");
    public string? ScrnMrkSeq => GetField("SCRN_MRK_SEQ");
    public string? UseYn => GetField("USE_YN");
    public string? Dtrl1Nm => GetField("DTRL1_NM");
    public string? Dtrl2Nm => GetField("DTRL2_NM");
    public string? Dtrl3Nm => GetField("DTRL3_NM");
    public string? Dtrl4Nm => GetField("DTRL4_NM");
    public string? Dtrl5Nm => GetField("DTRL5_NM");
    public string? Dtrl6Nm => GetField("DTRL6_NM");
    public string? Dtrl7Nm => GetField("DTRL7_NM");
    public string? Dtrl8Nm => GetField("DTRL8_NM");
    public string? NextgFmrComnCd => GetField("NEXTG_FMR_COMN_CD");

    private string? GetField(string name) => Fields.TryGetValue(name, out var value) ? value : null;
}
