using UnityEngine;

// 궤도 무기가 우주선 주위를 도는 움직임
public class MoveInCircle : MonoBehaviour
{
    public Transform mainPosition;

    [SerializeField]
    public float rotationRadius, angularSpeed;

    float angle = 0f;   // 라디안
    [HideInInspector] public MoveInCircle follow;   // 위성은 본체 각도를 따라가요
    [HideInInspector] public float phase;   // 증강 "위성 추가": 위성마다 다른 자리 (라디안)

    void Start()
    {
        if(mainPosition == null) {
            var ship = GameObject.FindGameObjectWithTag("MainWeapon");
            if(ship != null) mainPosition = ship.transform;
        }
    }

    void Update()
    {
        if(mainPosition == null) return;

        // 예전 코드는 쿼터니언의 z 성분(각도가 아님)을 넣어서 사실상 0도였어요. 같은 모습으로 고정해요.
        transform.rotation = Quaternion.identity;

        var orbital = Orbital.instance;
        float range = (orbital != null && orbital.extraRange ? 1.5f : 1f) * Tuning.Get("orbital_radius_mul", 1.3f);   // 우주선에서 떨어진 거리 (tuning.txt)
        float speed = (orbital != null && orbital.extraSpeed ? 1.5f : 1f);   // (궤도는 드론으로 바뀌어서 이 스크립트는 꺼져 있어요)

        transform.position = new Vector2(
            mainPosition.position.x + Mathf.Cos(angle + phase) * rotationRadius * range,
            mainPosition.position.y + Mathf.Sin(angle + phase) * rotationRadius * range);

        // 한 바퀴(2π)에서 0으로 (예전엔 360 라디안에서 되돌려서 그때마다 위치가 순간이동했어요)
        if(follow != null) { angle = follow.angle; return; }
        angle = Mathf.Repeat(angle + Time.deltaTime * angularSpeed * speed, Mathf.PI * 2f);
    }
}
