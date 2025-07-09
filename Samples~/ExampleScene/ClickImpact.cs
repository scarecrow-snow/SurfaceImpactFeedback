using System.Collections;
using System.Collections.Generic;
using SCLib_SurfaceImpactFeedback;
using UnityEngine;

public class ClickImpact : MonoBehaviour
{
    [SerializeField] ImpactType impactType;
    void Update()
    {
        
        if (Input.GetMouseButtonDown(0))
        {
            // マウスのスクリーン座標を取得
            Vector3 mousePosition = Input.mousePosition;
            
            // カメラからレイを発射
            Ray ray = Camera.main.ScreenPointToRay(mousePosition);
            
            RaycastHit hit;
            
            // レイキャストを実行して、何かに当たったかどうかを確認
            if (Physics.Raycast(ray, out hit))
            {
                // ヒットした情報からインパクトエフェクトを発生させる
                SurfaceImpactFeedback.Instance.HandleImpact(hit.collider.gameObject, hit.point, hit.normal, impactType);
            }
        }
    }
}
