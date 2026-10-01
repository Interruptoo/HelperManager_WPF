namespace HelperManager.Modules.RequestInfo.Models;

/// <summary>
/// 사용자가 선택한 폴더에서 발견된 로그 파일 하나에 대한 요약 정보입니다.
/// (파일 목록 콤보박스에 표시하기 위한 용도)
/// </summary>
/// <param name="FullPath">파일 전체 경로.</param>
/// <param name="FileName">파일명(확장자 포함).</param>
/// <param name="CreatedTime">파일 생성 시각. 생성일 기준 기간 필터링에 사용된다.</param>
/// <param name="LastWriteTime">마지막 수정 시각. 목록 정렬 및 표시용.</param>
/// <param name="SizeBytes">파일 크기(byte).</param>
public sealed record LogFileInfo(string FullPath, string FileName, DateTime CreatedTime, DateTime LastWriteTime, long SizeBytes)
{
    /// <summary>콤보박스/목록에 표시할 문자열. (파일명 + 수정시각 + 크기)</summary>
    public string DisplayText => $"{FileName}   [{LastWriteTime:yyyy-MM-dd HH:mm:ss}]   {SizeBytes / 1024.0:N1} KB";
}
