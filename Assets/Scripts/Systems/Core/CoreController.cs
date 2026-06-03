// 코어 컨트롤러 — 코어 GameObject와 CoreModel 연결, 월드 위치 제공
using UnityEngine;
using DefenseDot.Domain.Models;

namespace DefenseDot.Systems.Core
{
    /// <summary>
    /// 코어(본진) GameObject를 CoreModel과 연결하는 컨트롤러입니다.
    /// 코어의 월드 위치(아레나 공전 중심 / TD 코어 위치)를 제공하고, 피해 적용을 모델에 위임합니다.
    /// </summary>
    public class CoreController : MonoBehaviour
    {
        private CoreModel model;

        /// <summary>
        /// 코어의 현재 월드 위치입니다.
        /// </summary>
        public Vector3 CorePosition => transform.position;

        /// <summary>
        /// 코어 모델을 주입합니다. (GameManager가 호출)
        /// </summary>
        public void Bind(CoreModel coreModel)
        {
            model = coreModel;
        }

        /// <summary>
        /// 코어에 피해를 적용합니다. (모델에 위임)
        /// </summary>
        public void ApplyDamage(float amount)
        {
            model?.ApplyDamage(amount);
        }
    }
}
