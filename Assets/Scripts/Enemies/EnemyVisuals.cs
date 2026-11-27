using System.Collections.Generic;
using UnityEngine;

// Everything the player SEES an enemy do. Uses the Animator when the model has a real clip for an
// action, and falls back to procedural motion when it does not (the skeleton model only ships with
// idle/walk/run, and the cannon has no Animator at all).
[DisallowMultipleComponent]
public class EnemyVisuals : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Animator animator;
    private bool hasSpeed;
    private bool hasAttackState;
    private bool hasDeathState;
    private Transform chestBone;
    private Transform armBone;
    private Transform cannonBarrel;
    private Vector3 cannonBarrelRest;
    private Transform muzzle;
    private ParticleSystem muzzleFlash;
    private Renderer[] renderers;
    private MaterialPropertyBlock block;

    private float attackStart = -10f;
    private float attackWindup = 0.4f;
    private bool attacking;
    private float flinchStart = -10f;
    private float recoilStart = -10f;
    private float deathStart = -1f;
    private Quaternion deathStartRotation;
    private Vector3 deathStartPosition;
    private Color tintColor;
    private float tintUntil;
    private bool tinted;
    private float telegraphUntil;
    private Color telegraphColor;

    // The point projectiles come out of (the cannon's muzzle if it has one).
    public Vector3 MuzzlePosition
    {
        get
        {
            if (muzzle != null)
            {
                return muzzle.position;
            }
            float size = Mathf.Max(1f, transform.lossyScale.y);
            return transform.position + Vector3.up * 1.2f * size + transform.forward * 0.9f * size;
        }
    }

    // Finds the Animator, the bones used for procedural attacks, and the cannon's moving parts.
    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            // Root motion made the golem drift away from where the AI was moving it.
            animator.applyRootMotion = false;
            hasSpeed = HasParameter("Speed", AnimatorControllerParameterType.Float);
            hasAttackState = HasState("Attack") && HasParameter("Attack", AnimatorControllerParameterType.Trigger);
            hasDeathState = HasState("Death") && HasParameter("Dead", AnimatorControllerParameterType.Trigger);
        }

        chestBone = FindDeep(transform, "Chest_M", "Spine1_M", "B-chest", "B-spine");
        armBone = FindDeep(transform, "Shoulder_R", "B-upperArm.R");
        cannonBarrel = FindDeep(transform, "CannonMachine_Cannon");
        if (cannonBarrel != null)
        {
            cannonBarrelRest = cannonBarrel.localPosition;
        }
        Transform flash = FindDeep(transform, "CannonMuzzleFlashEffect");
        if (flash != null)
        {
            muzzleFlash = flash.GetComponent<ParticleSystem>();
            muzzle = flash;
        }

        List<Renderer> meshes = new List<Renderer>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is MeshRenderer || r is SkinnedMeshRenderer)
            {
                meshes.Add(r);
            }
        }
        renderers = meshes.ToArray();
        block = new MaterialPropertyBlock();
    }

    // Sets the walk/run blend (0 = standing still, 1 = full speed).
    public void SetLocomotion(float speed01)
    {
        if (animator != null && hasSpeed && animator.isActiveAndEnabled)
        {
            animator.SetFloat(SpeedHash, Mathf.Clamp01(speed01), 0.1f, Time.deltaTime);
        }
    }

    // Plays the attack. windup is how long until the hit lands, so the procedural swing lines up with the damage.
    public void PlayAttack(float windup)
    {
        attackWindup = Mathf.Max(0.1f, windup);
        attackStart = Time.time;
        attacking = true;
        if (hasAttackState)
        {
            animator.SetTrigger("Attack");
        }
    }

    // Glows a colour while an attack winds up, so the player can see it coming and react.
    public void Telegraph(Color color, float duration)
    {
        telegraphColor = color;
        telegraphUntil = Time.time + duration;
    }

    // Flashes red and flinches when the enemy is hurt.
    public void PlayHit()
    {
        Flash(new Color(1f, 0.35f, 0.35f), 0.15f);
        flinchStart = Time.time;
        if (animator != null && HasParameter("Hit", AnimatorControllerParameterType.Trigger) && HasState("Hit"))
        {
            animator.SetTrigger("Hit");
        }
    }

    // Flashes blue-white when a hit is blocked.
    public void PlayBlock()
    {
        Flash(new Color(0.55f, 0.75f, 1f), 0.2f);
        flinchStart = Time.time - 0.05f;
    }

    // Kicks the cannon barrel back and plays its muzzle flash.
    public void PlayFire()
    {
        recoilStart = Time.time;
        if (muzzleFlash != null)
        {
            muzzleFlash.Play(true);
        }
        if (!hasAttackState)
        {
            attacking = false;
        }
    }

    // Plays the death animation clip if the model has one, otherwise topples the model over.
    public void PlayDeath()
    {
        attacking = false;
        telegraphUntil = 0f;
        SetLocomotion(0f);
        deathStart = Time.time;
        deathStartRotation = transform.rotation;
        deathStartPosition = transform.position;
        Flash(new Color(0.6f, 0.6f, 0.6f), 0.3f);
        if (hasDeathState)
        {
            animator.SetTrigger("Dead");
        }
        else if (animator != null)
        {
            animator.enabled = false;
        }
    }

    // Applies procedural poses AFTER the Animator has posed the bones this frame.
    private void LateUpdate()
    {
        if (deathStart >= 0f)
        {
            UpdateDeath();
        }
        else
        {
            UpdateProceduralAttack();
            UpdateFlinch();
            UpdateRecoil();
        }
        UpdateTint();
    }

    // Leans back while winding up, then swings the chest and right arm down through the hit.
    private void UpdateProceduralAttack()
    {
        if (!attacking)
        {
            return;
        }
        float t = Time.time - attackStart;
        float strikeTime = 0.15f;
        float recoverTime = 0.35f;
        if (t > attackWindup + strikeTime + recoverTime)
        {
            attacking = false;
            return;
        }
        if (hasAttackState || chestBone == null)
        {
            return;
        }

        float chest;
        float arm;
        if (t < attackWindup)
        {
            float k = Smooth(t / attackWindup);
            chest = Mathf.Lerp(0f, -12f, k);
            arm = Mathf.Lerp(0f, -150f, k);
        }
        else if (t < attackWindup + strikeTime)
        {
            float k = Smooth((t - attackWindup) / strikeTime);
            chest = Mathf.Lerp(-12f, 25f, k);
            arm = Mathf.Lerp(-150f, -30f, k);
        }
        else
        {
            float k = Smooth((t - attackWindup - strikeTime) / recoverTime);
            chest = Mathf.Lerp(25f, 0f, k);
            arm = Mathf.Lerp(-30f, 0f, k);
        }

        Vector3 right = transform.right;
        chestBone.rotation = Quaternion.AngleAxis(chest, right) * chestBone.rotation;
        if (armBone != null)
        {
            armBone.rotation = Quaternion.AngleAxis(arm, right) * armBone.rotation;
        }
    }

    // Jerks the upper body back for a moment after being hit.
    private void UpdateFlinch()
    {
        float t = Time.time - flinchStart;
        if (t > 0.3f || chestBone == null)
        {
            return;
        }
        float angle = -18f * Mathf.Sin(Mathf.Clamp01(t / 0.3f) * Mathf.PI);
        chestBone.rotation = Quaternion.AngleAxis(angle, transform.right) * chestBone.rotation;
    }

    // Slides the cannon barrel back when it fires and eases it forward again.
    private void UpdateRecoil()
    {
        if (cannonBarrel == null)
        {
            return;
        }
        float t = Time.time - recoilStart;
        float amount = t < 0.05f ? t / 0.05f : Mathf.Clamp01(1f - (t - 0.05f) / 0.3f);
        Vector3 back = cannonBarrel.parent != null
            ? cannonBarrel.parent.InverseTransformDirection(-transform.forward)
            : -transform.forward;
        cannonBarrel.localPosition = cannonBarrelRest + back * (0.25f * amount);
    }

    // Topples the model (if it has no death clip), then sinks it into the floor before it is removed.
    private void UpdateDeath()
    {
        float t = Time.time - deathStart;
        if (!hasDeathState)
        {
            bool isMachine = cannonBarrel != null;
            float k = Smooth(Mathf.Clamp01(t / 0.6f));
            float angle = isMachine ? 25f : -85f;
            transform.rotation = Quaternion.AngleAxis(angle * k, deathStartRotation * Vector3.right) * deathStartRotation;
        }
        if (t > 1.8f)
        {
            float sink = Smooth(Mathf.Clamp01((t - 1.8f) / 1.2f));
            float depth = 1.5f * Mathf.Max(1f, transform.lossyScale.y);
            transform.position = new Vector3(transform.position.x, deathStartPosition.y - depth * sink, transform.position.z);
        }
    }

    // Starts a colour flash for a short time.
    private void Flash(Color color, float duration)
    {
        tintColor = color;
        tintUntil = Time.time + duration;
    }

    // Applies (or clears) the flash/telegraph colour on every mesh using a property block,
    // so materials are never duplicated.
    private void UpdateTint()
    {
        Color? wanted = null;
        if (Time.time < tintUntil)
        {
            wanted = tintColor;
        }
        else if (Time.time < telegraphUntil)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 20f);
            wanted = Color.Lerp(Color.white, telegraphColor, 0.4f + 0.4f * pulse);
        }

        if (wanted.HasValue)
        {
            block.SetColor(BaseColorId, wanted.Value);
            block.SetColor(ColorId, wanted.Value);
            foreach (Renderer r in renderers)
            {
                if (r != null)
                {
                    r.SetPropertyBlock(block);
                }
            }
            tinted = true;
        }
        else if (tinted)
        {
            foreach (Renderer r in renderers)
            {
                if (r != null)
                {
                    r.SetPropertyBlock(null);
                }
            }
            tinted = false;
        }
    }

    // Eases a 0-1 value in and out.
    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    // Checks if the Animator has a parameter with this name and type.
    private bool HasParameter(string parameterName, AnimatorControllerParameterType type)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return false;
        }
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == type)
            {
                return true;
            }
        }
        return false;
    }

    // Checks if the Animator's base layer has a state with this name.
    private bool HasState(string stateName)
    {
        return animator != null && animator.runtimeAnimatorController != null &&
               animator.HasState(0, Animator.StringToHash("Base Layer." + stateName));
    }

    // Searches the whole hierarchy for the first child matching any of the names.
    private static Transform FindDeep(Transform root, params string[] names)
    {
        foreach (string targetName in names)
        {
            Transform found = FindDeepSingle(root, targetName);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    // Searches the hierarchy for a child with exactly this name.
    private static Transform FindDeepSingle(Transform parent, string targetName)
    {
        if (parent.name == targetName)
        {
            return parent;
        }
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform match = FindDeepSingle(parent.GetChild(i), targetName);
            if (match != null)
            {
                return match;
            }
        }
        return null;
    }
}
