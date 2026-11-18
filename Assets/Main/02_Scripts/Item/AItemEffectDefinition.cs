using UnityEngine;

// 설정 에셋에는 조정값만 보관하고, 횟수·남은 시간 등은 생성한 효과 인스턴스에서 관리합니다.
public abstract class AItemEffectDefinition : ScriptableObject
{
    [SerializeField] private int _priority;

    public virtual int Priority => _priority;
    public virtual bool RequiresOwner => false;
    public virtual bool ReplaceExisting => false;

    public abstract AItemEffect CreateEffect(ItemData item, ItemEffectContext context);

    public virtual void Validate() { }
}
