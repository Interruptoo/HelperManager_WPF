namespace HelperManager.Modules.ComnCode.Models;

/// <summary>
/// 공통코드 그룹(ComnCdInfo)에 묶여 있는 상세 코드(ComnCdDetail)를 골라내는 규칙입니다.
///
/// Common Code 화면과 Table Info 화면이 같은 규칙을 써야 해서 한 곳에 모아 두었다.
/// (HSP_TP_CD 를 느슨하게 비교해야 하는 사정이 있어서, 양쪽에 따로 두면 한쪽만 고쳐질 위험이 크다)
/// </summary>
public static class ComnCodeGroupMatch
{
    /// <summary>지정한 그룹에 속한 상세 코드만 골라낸다. (TABLE_NAME + HSP_TP_CD + COMN_GRP_CD 기준)</summary>
    public static IEnumerable<ComnCodeRecord> DetailsOf(
        IEnumerable<ComnCodeRecord> details,
        ComnCodeRecord group) =>
        details.Where(record =>
            string.Equals(record.TableName, group.TableName, StringComparison.OrdinalIgnoreCase) &&
            HspTpCdMatches(record.HspTpCd, group.HspTpCd) &&
            string.Equals(record.ComnGrpCd, group.ComnGrpCd, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// ComnCdDetail.json 을 뽑는 쿼리는 원본 테이블(CCCCCSTE/CCCMCSTE 등)과 상관없이 HSP_TP_CD를
    /// 항상 빈 값으로 고정해서 내보낸다. 반면 ComnCdInfo.json 쪽은 병원별 공통코드 테이블
    /// (예: CCCMCSTE)의 경우 실제 HSP_TP_CD 값('01' 등)이 들어있다. 그래서 두 값을 그대로 엄격히
    /// 비교하면, HSP_TP_CD가 채워져 있는 Info 그룹(CCCMCSTE 등)은 Detail과 절대 일치하지 않아
    /// 상세 목록이 비어 보이는 문제가 있었다. 둘 중 하나라도 값이 없으면(= 어느 쪽이든 구분하지
    /// 않는다는 의미로 보고) 그 항목은 조건을 통과시킨다.
    /// </summary>
    private static bool HspTpCdMatches(string? detailValue, string? groupValue)
    {
        if (string.IsNullOrEmpty(detailValue) || string.IsNullOrEmpty(groupValue))
        {
            return true;
        }

        return string.Equals(detailValue, groupValue, StringComparison.OrdinalIgnoreCase);
    }
}
