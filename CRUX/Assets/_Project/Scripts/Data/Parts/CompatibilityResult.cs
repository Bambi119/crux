using System;

namespace Crux.Data
{
    /// <summary>
    /// 호환성 검사 결과 — docs/05 §3 3중 체계.
    /// 세 축(하중·출력·규격) 검사 결과를 단일 구조체로 표현.
    /// isValid=true면 장착 가능, false면 violations 배열에 사유 기재.
    /// </summary>
    public struct CompatibilityResult
    {
        /// <summary>
        /// 호환성 통과 여부.
        /// true = 모든 축 통과, false = 하나 이상의 위반.
        /// </summary>
        public bool isValid;

        /// <summary>
        /// 위반 사유 목록. 비어있으면 통과, 그 외는 사용자 UI 표시용.
        /// 각 항목은 "축명: 구체 사유" 형식.
        /// 예: "하중 초과: 총 85kg / 용량 60kg — 초과 25kg"
        /// </summary>
        public string[] violations;

        /// <summary>
        /// 통과 상태 미리 정의 상수.
        /// violations는 empty 배열로 초기화하고 isValid=true.
        /// </summary>
        public static CompatibilityResult Ok =>
            new CompatibilityResult
            {
                isValid = true,
                violations = System.Array.Empty<string>()
            };

        /// <summary>
        /// 지정된 위반 메시지로 실패 상태 생성.
        /// </summary>
        public static CompatibilityResult Fail(params string[] reasons) =>
            new CompatibilityResult
            {
                isValid = false,
                violations = reasons ?? System.Array.Empty<string>()
            };
    }
}
