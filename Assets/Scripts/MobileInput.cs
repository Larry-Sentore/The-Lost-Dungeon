using UnityEngine;

public static class MobileInput
{
    public static Vector2 ReadMovement()
    {
        Vector2 result = Vector2.zero;
        foreach (Touch touch in Input.touches)
        {
            if (touch.position.x < Screen.width * 0.5f)
            {
                Vector2 delta = touch.deltaPosition * 0.02f;
                result += delta;
            }
        }
        return Vector2.ClampMagnitude(result, 1f);
    }

    public static bool AttackPressed
    {
        get
        {
            foreach (Touch touch in Input.touches)
            {
                if (touch.position.x >= Screen.width * 0.5f && touch.phase == TouchPhase.Began)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public static bool AbilityOnePressed
    {
        get
        {
            foreach (Touch touch in Input.touches)
            {
                if (touch.phase == TouchPhase.Began && touch.position.x > Screen.width * 0.75f &&
                    touch.position.y > Screen.height * 0.25f)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public static bool AbilityTwoPressed
    {
        get
        {
            foreach (Touch touch in Input.touches)
            {
                if (touch.phase == TouchPhase.Began && touch.position.x > Screen.width * 0.75f &&
                    touch.position.y <= Screen.height * 0.25f)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
