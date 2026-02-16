// 리소스 타입 정의 - 프로젝트에서 로드할 수 있는 에셋 타입들
public enum AssetType
{
    Prefab,      // GameObject Prefab
    Sprite,      // UI Sprite
}

// 인스턴스 생성 시 설계 정의
public enum UnityStructureType
{
    OOP,    // 기존 객체지향 방식으로 GameObject를 생성
    ECS     // 데이터 지향 방식으로 SubScene에 Entity를 생성
}
