using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Domain.Models;
using DefenseDot.UI.Models;
using DefenseDot.UI.Views;
using DefenseDot.UI.Presenters;

namespace DefenseDot.UI.InGame
{
    /// <summary>
    /// UI 계층의 단일 합성 루트입니다. 합성 루트가 주입한 모델로 모든 프레젠터를 조립·관리합니다.
    /// 새 UI 추가 시 View 참조와 프레젠터 생성 한 줄만 더합니다.
    /// </summary>
    public class UIRoot : MonoBehaviour
    {
        [Header("Views")]
        [SerializeField] private HUDView hudView;

        private readonly List<IPresenter> presenters = new List<IPresenter>();

        /// <summary>
        /// 합성 루트가 도메인 모델과 적 수용 한계를 주입합니다.
        /// </summary>
        public void Inject(EconomyModel economy, CoreModel core, WaveModel wave, int enemyCapacity)
        {
            presenters.Add(new HUDPresenter(hudView, new HUDModel(), economy, core, wave, enemyCapacity));
            // 새 UI 는 여기에 추가

            foreach (IPresenter presenter in presenters) presenter.Initialize();
        }

        private void OnDestroy()
        {
            foreach (IPresenter presenter in presenters) presenter.Dispose();
            presenters.Clear();
        }
    }
}
