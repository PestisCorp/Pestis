using System;
using UnityEngine;

/// <summary>
/// Component attached to each rat so that clicking a Rat selects the horde it belongs to
/// </summary>
public class RatController : MonoBehaviour
{
    public Sprite DirectionUp;
    public Sprite DirectionUpLeft;
    public Sprite DirectionUpRight;
    public Sprite DirectionLeft;
    public Sprite DirectionRight;
    public Sprite DirectionDownLeft;
    public Sprite DirectionDown;
    public Sprite DirectionDownRight;

    private SpriteRenderer _spriteRenderer;

    private Rigidbody2D _rigidbody;

    public void Start()
    {
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _rigidbody = GetComponent<Rigidbody2D>();
        _rigidbody.mass = UnityEngine.Random.Range(0.8f, 1.2f);
    }

    void Update()
    {
        float angle = Vector2.SignedAngle(transform.up, Vector2.up);
        
        // Normalise to clockwise
        if (angle < 0)
        {
            angle += 360f;
        }

        if (angle < 22.5f)
        {
            _spriteRenderer.sprite = DirectionUp;
        } else if (angle < 67.5)
        {
            _spriteRenderer.sprite = DirectionUpRight;
        } else if (angle < 112.5)
        {
            _spriteRenderer.sprite = DirectionRight;
        } else if (angle < 157.5)
        {
            _spriteRenderer.sprite = DirectionDownRight;
        } else if (angle < 202.5)
        {
            _spriteRenderer.sprite = DirectionDown;
        } else if (angle < 247.5)
        {
            _spriteRenderer.sprite = DirectionDownLeft;
        } else if (angle < 292.5)
        {
            _spriteRenderer.sprite = DirectionLeft;
        }
        else
        {
            _spriteRenderer.sprite = DirectionUpLeft;
        }
        _spriteRenderer.transform.localRotation = Quaternion.Euler(new Vector3(0,0,angle));
    }
    
    private void OnMouseDown()
    {
        HordeController hordeController = GetComponentInParent<HordeController>();
        HumanPlayer player = hordeController.GetComponentInParent<HumanPlayer>();
        player.SelectHorde(hordeController);
    }
    
    /// <param name="force">The force to apply to the rat</param>
    /// <returns>New velocity</returns>
    public Vector2 AddForce(Vector2 force)
    {
        _rigidbody.AddForce(force);
        _rigidbody.linearVelocity = Vector2.ClampMagnitude(_rigidbody.linearVelocity, 0.2f);
        return _rigidbody.linearVelocity;
    }
}
