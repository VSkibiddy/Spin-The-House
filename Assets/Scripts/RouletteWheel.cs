using System;
using System.Collections;
using UnityEngine;

/// Builds a 37-pocket European wheel from primitives at runtime and spins a ball on it.
/// Spin phases:
///   1. Drop-in:  ball falls onto the rim track
///   2. Orbit:    ball circles the rim against the wheel (slow -> fast -> slow), moved by script
///   3. Release:  ball becomes a normal physics object, falls into the pockets, bounces over dividers
///   4. Settle:   wheel coasts to a stop, the pocket trigger the ball rests in is the result
/// Put this on an empty GameObject at (0,0,0), scale 1, with a Rigidbody (it is set kinematic here).
[RequireComponent(typeof(Rigidbody))]
public class RouletteWheel : MonoBehaviour
{
    public static readonly int[] Order = { 0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23, 10, 5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26 };
    static readonly int[] Reds = { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 };
    public static bool IsRed(int n) => Array.IndexOf(Reds, n) >= 0;

    [Header("Materials")]
    public Material redMat, blackMat, greenMat, crackedMat, wallMat, floorMat;
    public Material turretMat;   // brass for the center turret (falls back to the wall material)
    public Material coneMat;     // wood for the center cone (falls back to the wall material)

    [Header("Size")]
    public float innerRadius = 2.0f;
    public float outerRadius = 2.5f;

    [Header("Spin")]
    public Rigidbody ball;
    public float spinDuration = 12f;      // seconds until the wheel stops
    public float accelTime = 2f;          // slow -> fast phase
    public float wheelMaxSpeed = 180f;    // degrees per second
    public float ballMaxSpeed = 400f;     // degrees per second, orbiting the other way
    public float ballReleaseSpeed = 60f;  // degrees per second when the ball drops into the pockets
    public float releaseBeforeEnd = 4f;   // ball drops this many seconds before the wheel stops
    public float dropHeight = 1.5f;       // where the ball appears before falling onto the track
    public float trackHeight = 0.35f;     // height of the rim track the ball orbits on
    public float outwardForce = 0.8f;     // fakes the slope of a real bowl so the ball stays in the pocket ring
    public float spiralInward = 0.08f;    // how far the ball drifts in from the wall as it slows (0 = stays on the rim)

    [Header("Sound")]
    public AudioSource rollAudio;         // own Audio Source that plays the rolling sound
    public AudioClip rollClip;            // the looping rolling sound itself
    public AudioClip landClip;            // optional "clack" when the ball touches down
    [Range(0f, 1f)] public float rollVolume = 0.6f;
    public float settleTimeout = 3f;      // extra seconds after the wheel stops before forcing a result

    [HideInInspector] public float speedMultiplier = 1f;
    [HideInInspector] public Pocket lastPocket;

    public bool Spinning { get; private set; }

    Rigidbody rb;
    Pocket[] pockets;
    float currentSpeed;      // wheel, deg/sec
    bool ballFree;           // false = script moves the ball, true = physics moves it
    bool snapping;           // true while the ball slides into its final pocket
    float ballAngle;         // deg
    float ballSpeedDeg;      // deg/sec
    float ballY;
    float trackRadius;
    float ballRadiusNow;     // current orbit radius, shrinks as the ball slows

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        trackRadius = outerRadius - 0.15f;
        Build();
        ball.gameObject.SetActive(false);
    }

    // ---------- Build ----------
    void Build()
    {
        float mid = (innerRadius + outerRadius) * 0.5f;
        float depth = outerRadius - innerRadius;

        // Floor (round visual, box collider)
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floor.name = "Floor";
        floor.transform.SetParent(transform, false);
        floor.transform.localPosition = new Vector3(0, -0.05f, 0);
        floor.transform.localScale = new Vector3(outerRadius * 2f - 0.02f, 0.05f, outerRadius * 2f - 0.02f);   // edge tucked under the outer wall, never sticks out
        if (floorMat) floor.GetComponent<Renderer>().sharedMaterial = floorMat;
        Destroy(floor.GetComponent<Collider>());
        var fc = floor.AddComponent<BoxCollider>();
        fc.size = new Vector3(1f, 2f, 1f);

        // Inner and outer walls
        Ring("Inner", innerRadius, 0.3f, 32);
        Ring("Outer", outerRadius, 0.5f, 64);

        Turret();

        // Pockets and low dividers
        int n = Order.Length;
        float step = 360f / n;
        float width = 2f * Mathf.PI * mid / n;
        pockets = new Pocket[n];
        for (int i = 0; i < n; i++)
        {
            float a = i * step;
            int num = Order[i];
            Material m = num == 0 ? greenMat : (IsRed(num) ? redMat : blackMat);

            var pg = Box("Pocket " + num, Radial(a, mid, 0.011f), Rot(a), new Vector3(depth, 0.02f, width * 0.95f), m);
            var bc = pg.GetComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(1f, 15f, 1f);     // 0.3 units tall in world space
            bc.center = new Vector3(0f, 7.5f, 0f);
            var p = pg.AddComponent<Pocket>();
            p.number = num; p.wheel = this; p.rend = pg.GetComponent<Renderer>(); p.baseMat = m;
            pockets[i] = p;

            // Low divider: a fast ball hops over it, a slow ball gets caught
            Box("Divider", Radial(a + step * 0.5f, mid, 0.02f), Rot(a + step * 0.5f), new Vector3(depth, 0.04f, 0.03f), wallMat);
        }
    }

    // ---------- Center turret: smooth wooden cone with brass inlays, brass hub, dome and cross handle ----------
    [Header("Turret")]
    public int coneSegments = 8;          // wood segments separated by brass lines
    public float coneCenterHeight = 0.32f;
    public float coneEdgeHeight = 0.03f;

    float ConeH(float r, float R) => coneEdgeHeight + (coneCenterHeight - coneEdgeHeight) * (1f - r / R);

    void Turret()
    {
        Material wood = coneMat ? coneMat : wallMat;
        Material brass = turretMat ? turretMat : wallMat;
        float R = innerRadius - 0.025f;          // reaches the inner wall, no gap
        float hubR = 0.42f;

        // 1. Smooth cone mesh (one piece, radial UVs so wood grain runs nicely)
        var go = new GameObject("Cone");
        go.transform.SetParent(transform, false);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = wood;
        int seg = 96;
        var v = new Vector3[seg + 2];
        var uv = new Vector2[seg + 2];
        var tri = new int[seg * 3];
        v[0] = new Vector3(0f, ConeH(0f, R), 0f); uv[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            float x = Mathf.Cos(a) * R, z = Mathf.Sin(a) * R;
            v[i + 1] = new Vector3(x, coneEdgeHeight, z);
            uv[i + 1] = new Vector2(x / (2f * R) + 0.5f, z / (2f * R) + 0.5f);
        }
        for (int i = 0; i < seg; i++) { tri[i * 3] = 0; tri[i * 3 + 1] = i + 2; tri[i * 3 + 2] = i + 1; }
        var mesh = new Mesh { name = "Cone", vertices = v, uv = uv, triangles = tri };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        mf.mesh = mesh;

        // 2. Brass inlay lines between the wood segments
        float slope = Mathf.Atan2(coneCenterHeight - coneEdgeHeight, R) * Mathf.Rad2Deg;
        float len = R - hubR;
        float mid = hubR + len * 0.5f;
        for (int k = 0; k < coneSegments; k++)
        {
            float deg = k * 360f / coneSegments;
            Part(PrimitiveType.Cube, "Brass Line", Radial(deg, mid, ConeH(mid, R) + 0.006f),
                 new Vector3(len, 0.012f, 0.025f), Rot(deg) * Quaternion.Euler(0f, 0f, -slope), brass);
        }
        // Brass ring at the outer edge of the cone
        Part(PrimitiveType.Cylinder, "Brass Edge", new Vector3(0f, coneEdgeHeight - 0.005f, 0f),
             new Vector3(R * 2f + 0.02f, 0.012f, R * 2f + 0.02f), Quaternion.identity, brass);

        // 3. Hub: brass disc + dome
        float hubY = ConeH(hubR, R);
        Part(PrimitiveType.Cylinder, "Hub", new Vector3(0f, hubY + 0.02f, 0f),
             new Vector3(hubR * 2f, 0.05f, hubR * 2f), Quaternion.identity, brass);
        Part(PrimitiveType.Sphere, "Dome", new Vector3(0f, hubY + 0.07f, 0f),
             new Vector3(0.6f, 0.32f, 0.6f), Quaternion.identity, brass);

        // 4. Turret: post, top knob, four thick arms with ball ends
        float baseY = hubY + 0.2f;
        Part(PrimitiveType.Cylinder, "Turret Post", new Vector3(0f, baseY + 0.18f, 0f),
             new Vector3(0.16f, 0.2f, 0.16f), Quaternion.identity, brass);
        Part(PrimitiveType.Sphere, "Turret Knob", new Vector3(0f, baseY + 0.42f, 0f),
             new Vector3(0.24f, 0.24f, 0.24f), Quaternion.identity, brass);
        float armY = baseY + 0.12f;
        for (int k = 0; k < 4; k++)
        {
            Vector3 dir = Quaternion.Euler(0f, k * 90f + 45f, 0f) * Vector3.forward;
            Part(PrimitiveType.Cylinder, "Turret Arm", new Vector3(0f, armY, 0f) + dir * 0.33f,
                 new Vector3(0.08f, 0.33f, 0.08f), Quaternion.FromToRotation(Vector3.up, dir), brass);
            Part(PrimitiveType.Sphere, "Turret Arm Knob", new Vector3(0f, armY, 0f) + dir * 0.7f,
                 new Vector3(0.16f, 0.16f, 0.16f), Quaternion.identity, brass);
        }
    }

    GameObject Part(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Quaternion rot, Material m)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        Destroy(g.GetComponent<Collider>());   // decoration only, the ball never reaches the center
        g.transform.SetParent(transform, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = rot;
        g.transform.localScale = scale;
        if (m) g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    void Ring(string name, float r, float h, int segments)
    {
        float len = 2f * Mathf.PI * r / segments * 1.1f;
        for (int i = 0; i < segments; i++)
        {
            float a = i * 360f / segments;
            Box(name + " wall", Radial(a, r, h * 0.5f), Rot(a), new Vector3(0.05f, h, len), wallMat);
        }
    }

    Vector3 Radial(float deg, float r, float y)
    {
        float rad = deg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(rad) * r, y, Mathf.Sin(rad) * r);
    }

    Quaternion Rot(float deg) => Quaternion.Euler(0f, -deg, 0f);   // local X = radial, local Z = tangent

    GameObject Box(string name, Vector3 pos, Quaternion rot, Vector3 scale, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(transform, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = rot;
        g.transform.localScale = scale;
        if (m) g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    // ---------- House rule: cracked pockets ----------
    public void SetCracked(int count)
    {
        foreach (var p in pockets) { p.cracked = false; p.rend.sharedMaterial = p.baseMat; }
        for (int k = 0; k < count; k++)
        {
            var p = pockets[UnityEngine.Random.Range(1, pockets.Length)];   // never the zero pocket
            p.cracked = true;
            if (crackedMat) p.rend.sharedMaterial = crackedMat;
        }
    }

    public bool IsCracked(int number)
    {
        foreach (var p in pockets) if (p.number == number) return p.cracked;
        return false;
    }

    // ---------- Spin ----------
    public void Spin(Action<int> onResult)
    {
        if (!Spinning) StartCoroutine(SpinRoutine(onResult));
    }

    // Wheel speed: smooth ramp up during accelTime, smooth ramp down to 0 at spinDuration
    float WheelSpeedAt(float t)
    {
        float max = wheelMaxSpeed * speedMultiplier;
        if (t < accelTime) return max * Mathf.SmoothStep(0f, 1f, t / accelTime);
        if (t < spinDuration) return max * (1f - Mathf.SmoothStep(0f, 1f, (t - accelTime) / (spinDuration - accelTime)));
        return 0f;
    }

    IEnumerator SpinRoutine(Action<int> onResult)
    {
        Spinning = true;
        ballFree = false;
        lastPocket = null;

        float release = Mathf.Max(accelTime + 0.5f, spinDuration - releaseBeforeEnd);
        float ballMax = ballMaxSpeed * speedMultiplier;

        // Phase 1 + 2: script-driven drop-in and orbit
        ballAngle = UnityEngine.Random.Range(0f, 360f);
        ballY = dropHeight;
        ballRadiusNow = trackRadius;
        bool landed = false;
        if (rollAudio) { rollAudio.loop = true; rollAudio.playOnAwake = false; rollAudio.Stop(); }
        ball.gameObject.SetActive(true);
        ball.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        ball.isKinematic = true;
        ball.position = transform.position + Radial(ballAngle, trackRadius, ballY);

        float t = 0f;
        while (t < release)
        {
            t += Time.deltaTime;
            currentSpeed = WheelSpeedAt(t);

            if (t < accelTime) ballSpeedDeg = ballMax * Mathf.SmoothStep(0f, 1f, t / accelTime);
            else ballSpeedDeg = Mathf.Lerp(ballMax, ballReleaseSpeed, Mathf.SmoothStep(0f, 1f, (t - accelTime) / (release - accelTime)));

            float drop = Mathf.Clamp01(t / 0.6f);
            ballY = Mathf.Lerp(dropHeight, trackHeight, drop * drop);   // falls faster as it goes, like gravity

            // Spiral inward off the wall as it slows, with a small wobble
            float slow = Mathf.Clamp01((t - accelTime) / (release - accelTime));
            float wobble = Mathf.Sin(t * 9f) * 0.015f * (1f - slow);
            ballRadiusNow = trackRadius - spiralInward * Mathf.SmoothStep(0f, 1f, slow) + wobble;
            float br = ball.transform.localScale.x * 0.5f + 0.03f;                       // ball radius + small gap
            ballRadiusNow = Mathf.Clamp(ballRadiusNow, innerRadius + br, outerRadius - br); // never into a wall

            // Rolling sound starts when the ball touches down
            if (!landed && drop >= 1f)
            {
                landed = true;
                if (rollAudio)
                {
                    if (landClip) rollAudio.PlayOneShot(landClip);
                    if (rollClip) { rollAudio.clip = rollClip; rollAudio.Play(); }
                }
            }
            if (rollAudio && landed)
            {
                float k = ballSpeedDeg / ballMax;
                rollAudio.volume = rollVolume * Mathf.Lerp(0.35f, 1f, k);
                rollAudio.pitch = Mathf.Lerp(0.8f, 1.2f, k);
            }
            yield return null;
        }

        // Phase 3: hand the ball to physics with its current orbit velocity
        ballFree = true;
        lastPocket = null;
        ball.isKinematic = false;
        ball.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        float rad = ballAngle * Mathf.Deg2Rad;
        Vector3 tangent = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        ball.linearVelocity = tangent * ballRadiusNow * ballSpeedDeg * Mathf.Deg2Rad;   // use .velocity before Unity 6
        ball.angularVelocity = Vector3.zero;

        // Phase 4: wheel coasts to a stop, wait for the ball to rest in a pocket
        float settled = 0f;
        while (true)
        {
            t += Time.deltaTime;
            currentSpeed = WheelSpeedAt(t);
            bool still = ball.linearVelocity.magnitude < 0.15f && currentSpeed < 1f;
            if (rollAudio)   // rolling sound follows the real ball speed and fades out
            {
                float k = Mathf.Clamp01(ball.linearVelocity.magnitude / 2f);
                rollAudio.volume = Mathf.MoveTowards(rollAudio.volume, rollVolume * 0.35f * k, Time.deltaTime);
                rollAudio.pitch = Mathf.Lerp(0.7f, 0.9f, k);
            }
            settled = (lastPocket != null && still) ? settled + Time.deltaTime : 0f;
            if (settled > 0.5f || t > spinDuration + settleTimeout) break;
            yield return null;
        }

        currentSpeed = 0f;

        // Phase 5: slide the ball into the nearest pocket's center,
        // so it can never end up resting on a divider between two colors
        Pocket result = Nearest();
        snapping = true;
        ball.isKinematic = true;
        ball.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        Vector3 from = ball.position;
        Vector3 to = result.transform.position;
        to.y = transform.position.y + ball.transform.localScale.y * 0.5f;   // ball resting on the floor
        float s = 0f;
        while (s < 1f)
        {
            s += Time.fixedDeltaTime / 0.35f;
            ball.MovePosition(Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, s)));
            yield return new WaitForFixedUpdate();
        }
        snapping = false;
        if (rollAudio) rollAudio.Stop();

        Spinning = false;
        onResult(result.number);
    }

    void FixedUpdate()
    {
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, currentSpeed * Time.fixedDeltaTime, 0f));
        if (!Spinning || snapping) return;

        if (!ballFree)
        {
            // Orbit against the wheel's direction
            ballAngle += ballSpeedDeg * Time.fixedDeltaTime;
            ball.MovePosition(transform.position + Radial(ballAngle, ballRadiusNow, ballY));
        }
        else
        {
            Vector3 o = ball.position - transform.position; o.y = 0f;
            ball.AddForce(o.normalized * outwardForce, ForceMode.Acceleration);
        }
    }

    // Closest pocket to the ball: decides the final result and where the ball snaps to
    Pocket Nearest()
    {
        Pocket best = pockets[0]; float d = float.MaxValue;
        foreach (var p in pockets)
        {
            float dd = Vector3.Distance(p.transform.position, ball.position);
            if (dd < d) { d = dd; best = p; }
        }
        return best;
    }
}