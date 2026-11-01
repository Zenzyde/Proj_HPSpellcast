using UnityEngine;
using UnityEngine.InputSystem;

public class CamController : MonoBehaviour
{
	[SerializeField] private float sensitivity = 0.01f;

	WandController wandController;

	bool canRotate = false;

	void Awake()
	{
		Mouse.current.WarpCursorPosition(new Vector2(Screen.width / 2f, Screen.height / 2f));
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}

	void Start()
	{
		wandController = FindAnyObjectByType<WandController>();
		wandController.OnSpellRedirected += (isRedirecting) => canRotate = isRedirecting;
	}

	// Update is called once per frame
	void Update()
	{
		MoveCam();
		RotateCam();
	}

	void MoveCam()
	{
		Vector3 movement = Vector3.zero;
		movement.x += sensitivity * (Keyboard.current.aKey.isPressed ? -1f : 0f);
		movement.x += sensitivity * (Keyboard.current.dKey.isPressed ? 1f : 0f);
		movement.z += sensitivity * (Keyboard.current.wKey.isPressed ? 1f : 0f);
		movement.z += sensitivity * (Keyboard.current.sKey.isPressed ? -1f : 0f);

		transform.position += movement * sensitivity * Time.deltaTime;
	}

	void RotateCam()
	{
		if (!canRotate)
			return;
		Vector2 mouseDelta = Mouse.current.delta.ReadValue();

		Vector3 rotation = new Vector3(
			-mouseDelta.y,
			mouseDelta.x,
			0f
		);

		transform.eulerAngles += rotation * sensitivity * Time.deltaTime;
	}
}
