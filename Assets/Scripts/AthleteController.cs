﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
//using UnityEngine.EventSystems;

public class AthleteController : MonoBehaviour{

	private Athlete athlete;

	public bool crowdAthlete = false;

	//Variables
	private float tongueForce = 600f;
	
	private float tailStretch = 15;

	//Body Parts
	public List<SpriteRenderer> legList;
	public SpriteRenderer jersey;

	private Body body;
	private TailBody tailBody;
	private TailTip tailTip;
	private Face face;
	private Tongue tongue;
	private TextMeshPro jerseyText;

	private MatchController matchController;
	private CameraController cameraController;
	private AudioManager audioManager;
	private AudioSource audioSource;

	private Vector3 directionDragged = Vector3.zero;

	private Coroutine squishCoroutine;
	private Coroutine runningCoroutine;
	private Coroutine tongueCoroutine;

	private bool instantInteraction = false;
	private bool disabledInteraction = false;
	private bool moving = false;
	private bool ready = false;
	private bool dizzy = false;
	private float expressionTimer = 0f;
	private bool substitute = false;

	private Vector2 lastVelocity;
	private Vector3 originalScale;

	private List<Bumper> bumpersInside = new List<Bumper>();

	void Awake() {
		matchController = FindObjectOfType<MatchController>();
		cameraController = FindObjectOfType<CameraController>();
		audioManager = FindObjectOfType<AudioManager>();
		audioSource = GetComponent<AudioSource>();

		body = GetComponent<Body>();
		tailBody = GetComponentInChildren<TailBody>();
		tailTip = GetComponentInChildren<TailTip>();
		face = GetComponentInChildren<Face>();
		tongue = GetComponentInChildren<Tongue>();

		body.SetBody();
		tailBody.SetTailBody();
		tailTip.SetTailTip();
		face.SetFace();
		tongue.SetTongue();

		jerseyText = GetComponentInChildren<TextMeshPro>();

		SetInstantInteraction(true);

		originalScale = transform.localScale;
	}

	public void SetAthlete(Athlete a) {
		
		if(a == null) { //Set the athlete as a spectator
			athlete = new Athlete();
			float rando = Random.value;
			if(rando > 0.8) {
				athlete.SetTeam(matchController.GetTeam(false));
			} else {
				athlete.SetTeam(matchController.GetTeam(true));
			}

			jerseyText.text = "";
		} else {
			athlete = a;

			jerseyText.text = a.jerseyNumber.ToString();
		}

		body.SetSprite(athlete.athleteData.bodySprite);
		face.SetFaceBase(athlete.athleteData.faceBaseSprite);
		face.SetFaceSprite("neutral");
		
		if(jersey != null) {
			jersey.GetComponent<SpriteRenderer>().sprite = athlete.athleteData.athleteJersey;
		}

        for(int l = 0; l < legList.Count; l++) {
			/* 
			if(l < legList.Count / 2) { //If it's the first half of legs
				legList[l].GetComponent<SpriteRenderer>().sprite = athlete.athleteData.frontLegSprite;
			} else {
				legList[l].GetComponent<SpriteRenderer>().sprite = athlete.athleteData.backLegSprite;
			*/
			legList[l].GetComponent<SpriteRenderer>().sprite = athlete.athleteData.legSprite;
        }

		RetractLegs();

		RestoreAthleteColor();
	}

	public Athlete GetAthlete() {
		return athlete;
	}

	public void SetInstantInteraction(bool isTrue) {
		instantInteraction = isTrue;
	}

	void FixedUpdate() {

		Vector2 newVelocity = body.GetVelocity();
		if(lastVelocity.magnitude == 0) {
			if(newVelocity.magnitude > 0) {
				if(!moving) {
					StartedMoving();
				}
			}
		} else {
			Vector2 acceleration = (newVelocity - lastVelocity) / Time.deltaTime;

			if(acceleration.magnitude < 0.1f) {
				if(moving) {
					if(!dizzy) {
						StoppedMoving();
					}
				}
			}
		}
		lastVelocity = newVelocity;

		if(moving) {
			float step = newVelocity.magnitude / 2;
			float legWidth = legList[0].size.x;
			float newlegLength;

			for(int i = 0; i < legList.Count; i++) {
				if(i < legList.Count / 2) {
					newlegLength = Mathf.Lerp(athlete.athleteData.frontLegMin * legWidth, athlete.athleteData.frontLegMax * legWidth, step);
				} else {
					newlegLength = Mathf.Lerp(athlete.athleteData.backLegMin * legWidth, athlete.athleteData.backLegMax * legWidth, step);
				}

				legList[i].size = new Vector2(legList[i].size.x, newlegLength);
			}
		}

		if(expressionTimer > 0f) {
			//Do nothing while the coroutine goes
		} else {
			if(Mathf.Abs(body.GetAngularVelocity()) > 60f) {
				if(!dizzy) {
					StartedDizziness();
				}
			} else {
				if(dizzy) {
					StoppedDizziness();
				}
			}
		}
	}

	public void Collided(Collision2D collision) {
		audioManager.PlaySound("athleteBump");

		IncreaseStat(StatType.Bumps);
		
		if(collision.gameObject.CompareTag("Athlete")){
			//cameraController.AddTrauma(0.18f);

			if(collision.gameObject.GetComponent<AthleteController>().GetAthlete().GetTeam() != GetAthlete().GetTeam()) {
				if(moving) {
					IncreaseStat(StatType.Tackles);
				}
			}

			if(!dizzy) {
				if(collision.gameObject.GetComponent<AthleteController>().athlete.GetTeam() == athlete.GetTeam()) { //Teammate
					face.ChangeExpression("bumpedteam", 2f);
				} else { //Opponent
					face.ChangeExpression("bumpedenemy", 1.5f);
				}
			}
		} else {
			if(collision.gameObject.CompareTag("Ball")) {
				IncreaseStat(StatType.Touches);
			} /* else if(collision.gameObject.CompareTag("Bumper")) {
				IncreaseStat("bounces");
			}
			*/

			if(!dizzy) {
				face.ChangeExpression("bumped", 1f);
			}
		}

		//StartSquish();
	}

	public void DisableBody() {
		body.DisableBody();
	}

	public void EnableBody() {
		body.EnableBody();
	}

	public void DisableInteraction() {
		disabledInteraction = true;
	}

	public void IgnoreRaycasts(bool ignoring) {
		if(ignoring) {
			tailTip.gameObject.layer = 2;
		} else {
			tailTip.gameObject.layer = 0;
		}
	}

	public void DimAthleteColor() {
		Color teamColor = athlete.GetTeam().primaryColor;
		Color skinColor = athlete.skinColor;
		Color darkerSkinColor = athlete.bodySkinColor;
		Color targetColor = Color.black;
		float lerpValue = 0.5f;

		body.SetColor(Color.Lerp(darkerSkinColor, targetColor, lerpValue));
		tailTip.SetColor(Color.Lerp(darkerSkinColor, targetColor, lerpValue));

		face.SetColor(Color.Lerp(skinColor, targetColor, lerpValue));
		tailBody.SetColor(Color.Lerp(skinColor, targetColor, lerpValue));

		for(int i = 0; i < legList.Count; i++) {
			legList[i].GetComponent<SpriteRenderer>().color = Color.Lerp(skinColor, targetColor, lerpValue);
		}

		tongue.SetTongueColor(Color.Lerp(athlete.tongueColor, targetColor, lerpValue));

		if(jersey != null) {
			jersey.GetComponent<SpriteRenderer>().color = Color.Lerp(teamColor, targetColor, lerpValue);
			jerseyText.color = Color.Lerp(Color.white, targetColor, lerpValue);
		}
	}

	public void EnableInteraction() { //And Restore Color
		disabledInteraction = false;
	}

	public void RestoreAthleteColor() {
		Color teamColor = athlete.GetTeam().primaryColor;
		Color skinColor = athlete.skinColor;
		Color darkerSkinColor = athlete.bodySkinColor;

		body.SetColor(darkerSkinColor);
		tailTip.SetColor(darkerSkinColor);

		face.SetColor(skinColor);
		tailBody.SetColor(skinColor);

		for(int i = 0; i < legList.Count; i++) {
			legList[i].GetComponent<SpriteRenderer>().color = skinColor;
		}

		tongue.SetTongueColor(athlete.tongueColor);

		if(jersey != null) {
			jersey.color = teamColor;

			jerseyText.color = Color.white;
		}
	}

	public bool IsDisabled() {
		return disabledInteraction;
	}

	public void MouseEnter() {
		if(!crowdAthlete) {
			matchController.AthleteHovered(athlete);

			if(!disabledInteraction) {
				if(!matchController.GetAthleteBeingDragged()) {
					face.SetFaceSprite("hovered");
				}
			}
		}
	}

	public void MouseExit() {
		if(!crowdAthlete) {
			matchController.AthleteUnhovered(athlete);

			if(!disabledInteraction) {
				if(!matchController.GetAthleteBeingDragged() && !moving) {
					face.SetFaceSprite("neutral");
				}
			}
		}
	}

	public void MouseDrag() {
		TailAdjusted();
	}

	public void MouseClick() {
		//face.SetFaceSprite("dragging");
		if(!crowdAthlete) {
			if(!disabledInteraction) {
				GetComponent<SortingGroup>().sortingLayerName = "Focal Athlete";

				matchController.SetAthleteBeingDragged(true);

				ExtendLegs();

				//Unready();
			}
		}
	}

	public void TailAdjusted() {
		if(!disabledInteraction && !crowdAthlete) {
			Vector3 flatDirection = transform.position;
			flatDirection.z = 0f;
			Vector3 direction = (flatDirection - Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -Camera.main.transform.position.z)));

			if(direction.magnitude > athlete.minPull) {
				if(direction.magnitude >= athlete.maxPull) {
					direction = Vector3.ClampMagnitude(direction, athlete.maxPull);
				}

				Vector3 normalizedDirection = direction.normalized;	//Rotate the athlete to face opposite of the mouse
				float targetRotation = (Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg + 90);
				//float startRotation = transform.rotation.eulerAngles.z;
				transform.rotation = Quaternion.Euler(0, 0, targetRotation);

				tailTip.AdjustTailPosition(direction.magnitude);

				if(tongue.GetTongueOut()) {
					tongue.HideTongue();
				}

				face.SetFaceSprite("dragging");
			} else {
				direction = Vector3.zero;

				ResetTail();

				if(!tongue.GetTongueOut()) {
					tongue.RevealTongue();
				}

				face.SetFaceSprite("hovered");
			}

			directionDragged = direction;
		}
	}

	public void AdjustTailAndFling(Vector3 target, float percentFlingForce) {
		matchController.SetAthleteBeingDragged(true);

		ExtendLegs();
		Vector2 direction = target - transform.position;

		if(direction.magnitude > athlete.minPull) {
			if(direction.magnitude >= athlete.maxPull) {
				direction = Vector3.ClampMagnitude(direction, athlete.maxPull);
			}

			Vector3 normalizedDirection = direction.normalized;	//Rotate the athlete to face opposite of the mouse
			float targetRotation = (Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg + 90);
			//float startRotation = transform.rotation.eulerAngles.z;
			transform.rotation = Quaternion.Euler(0, 0, targetRotation);

			tailTip.AdjustTailPosition(direction.magnitude);

			if(tongue.GetTongueOut()) {
				tongue.HideTongue();
			}

			face.SetFaceSprite("dragging");
		} else {
			Debug.Log("Why are you even calling this function then with that weak ass tail force?");
		}

		directionDragged = direction;

		StartAction();
	}

	public void ResetTail() {
		tailTip.AdjustTailPosition(0f);
	}

	public void Unclicked() {
		if(!crowdAthlete) {
			matchController.SetAthleteBeingDragged(false);

			GetComponent<SortingGroup>().sortingLayerName = "Athletes";

			if(directionDragged != Vector3.zero) { //Athlete will move
				if(instantInteraction) {
					StartAction();
				} else {
					ReadyUp();
				}
			} else {
				face.SetFaceSprite("neutral");
				RetractLegs();
			}
		}
	}

	public void ReadyUp() {
		ready = true;

		face.SetFaceSprite("dragging");

		RetractLegs();

		if(runningCoroutine == null) {
			runningCoroutine = StartCoroutine(RunInPlace());
		}
	}

	/*
	public void CancelReady() {
		RetractLegs();

		if(!dizzy) {
			StartCoroutine(ChangeExpression(1f, "sad"));
		}

		Unready();
	}
	*/

	public void Unready() {
		ready = false;

		if(runningCoroutine != null) {
			StopCoroutine(runningCoroutine);
			runningCoroutine = null;
		}
	}

	public bool GetReady() {
		return ready;
	}

	public void StartAction() {
		Unready();

		matchController.Fling(this);

		if(directionDragged != Vector3.zero) {
			FlingAthlete();
		} else {
			if(tongue.GetTongueOut()) {
				FlickTongue();
			}
		}
	}

	public void FlingAthlete() {
		matchController.SetAthleteInitiater(this);
		IncreaseStat(StatType.Flings);

		Vector2 minDirectionDrag = directionDragged.normalized * athlete.minPull;
		Vector2 adjustedDirection = (Vector2)directionDragged - minDirectionDrag;
		if(adjustedDirection.magnitude < 0) {
			adjustedDirection = Vector2.zero;
		}

		//Debug.Log(adjustedDirection);

		Vector2 force = adjustedDirection * athlete.flingForce;
		body.AddForce(force);

		directionDragged = Vector3.zero;

		ResetTail();
	}

	public void IncreaseStat(StatType type) {
		if(matchController.IsTurnActive()) {
			athlete.IncreaseStat(type);

			if(matchController.GetAthleteInitiater() == this) {
				matchController.UpdateStats(type, athlete);
			}
		}
	}

	public void FlickTongue() {
		tongueCoroutine = StartCoroutine(tongue.ExtendTongue());
	}

	public void TongueGrabbed(GameObject obj) {
		if(tongueCoroutine != null) {
			StopCoroutine(tongueCoroutine);
			tongueCoroutine = null;
		}

		if(obj.GetComponent<Rigidbody2D>() != null) {
			obj.GetComponent<Rigidbody2D>().AddForce(-transform.up * tongueForce / 2);
		}

		StartCoroutine(tongue.RetractTongue());
		body.AddForce(transform.up * tongueForce);

		directionDragged = Vector3.zero;
	}

	public void StartSquish() {
		
		if(squishCoroutine == null) {
			squishCoroutine = StartCoroutine(Squish());
		}
	}

	public IEnumerator Squish() {
		Vector3 startScale = transform.localScale;
		Vector3 endScale = new Vector3(startScale.x * 1.1f, startScale.y * 0.9f, startScale.z);

		float timer = 0;
		float duration = 0.3f;
		WaitForFixedUpdate waiter = new WaitForFixedUpdate();
		while(timer < duration) {
			timer += Time.deltaTime;

			transform.localScale = Vector3.Lerp(startScale, endScale, timer / duration);

			yield return waiter;
		}

		transform.localScale = startScale;
		squishCoroutine = null;
	}

	public void StartedMoving() {
		moving = true;

		if(!face.IsExpressing()) {
			face.SetFaceSprite("going");
		}

		ExtendLegs();
	}
	public void StoppedMoving() {
		moving = false;

		if(!dizzy) {
			face.ChangeExpression("stopped", 1.5f);
		}
		
		Unready();

		RetractLegs();

		body.StopMovement();
	}

	public bool GetMoving() {
		return moving;
	}

	public void StartedDizziness() {
		dizzy = true;

		face.SetFaceSprite("dizzy");
	}

	public void StoppedDizziness() {
		dizzy = false;

		face.DetermineFaceState();
	}

	public bool GetDizzy() {
		return dizzy;
	}

	public void ExtendLegs() {
		float standardWidth = legList[0].size.x;
		for(int i = 0; i < legList.Count; i++) {
			if(i < legList.Count / 2) {
				legList[i].size = new Vector2(standardWidth, athlete.athleteData.frontLegMin * standardWidth);
			} else {
				legList[i].size = new Vector2(standardWidth, athlete.athleteData.backLegMin * standardWidth);
			}
		}
	}

	public void RetractLegs() {
		float standardWidth = legList[0].size.x;
		for(int i = 0; i < legList.Count; i++) {
			if(i < legList.Count / 2) {
				legList[i].size = new Vector2(standardWidth, athlete.athleteData.frontLegRest * standardWidth);
			} else {
				legList[i].size = new Vector2(standardWidth, athlete.athleteData.backLegRest * standardWidth);
			}
		}
	}

	public IEnumerator RunInPlace() {
		
		float rando = 0.05f + (Random.value / 3f);
		yield return new WaitForSeconds(rando);

		bool running = true;

		SpriteRenderer leftLeg = legList[0];
		SpriteRenderer rightLeg = legList[1];

		bool left = (Random.value > 0.5f);
		int countTillFlip = Random.Range(10, 18);
		int count = countTillFlip;

		WaitForFixedUpdate waiter = new WaitForFixedUpdate();
		while(running) {
			float step = (countTillFlip - count) / (float)countTillFlip;

			/*
			if(step <= 1) {
				if(left) {
					leftLeg.size = new Vector2(leftLeg.size.x, Mathf.Lerp(legRest, legMin, step));
				} else {
					rightLeg.size = new Vector2(rightLeg.size.x, Mathf.Lerp(legRest, legMin, step));
				}
			} else {
				if(left) {
					leftLeg.size = new Vector2(leftLeg.size.x, Mathf.Lerp(legMin, legRest, step - 1));
				} else {
					rightLeg.size = new Vector2(rightLeg.size.x, Mathf.Lerp(legMin, legRest, step - 1));
				}
			}
			*/

			count--;

			if(count == 0) {

			} else if(count <= -countTillFlip) {
				count = countTillFlip;
				left = !left;
			}

			yield return waiter;
		}
	}

	public void FinishMatch() {
		//if(GetTeam().
		//If team won, victory face, else defeat face
		if(athlete.GetTeam().wonTheGame) {
			face.SetFaceSprite("victory");
		} else {
			face.SetFaceSprite("defeat");
		}
	}

	public Face GetFace() {
		return face;
	}

	//Crowd Settings
	private GameObject focalObject;
	private Coroutine focusCoroutine;
	public void SetFocus(GameObject obj) {
		if(focusCoroutine != null) {
			StopCoroutine(focusCoroutine);
		}

		focusCoroutine = StartCoroutine(ChangeFocus(obj));
	}

	public IEnumerator ChangeFocus(GameObject newFocus) {
		
		Quaternion startQuaternion;
		if(focalObject != null) {
			startQuaternion = GetFocalQuaternion();
		} else {
			startQuaternion = Quaternion.identity;
		}

		focalObject = newFocus;
		Quaternion endQuaternion = GetFocalQuaternion();

		WaitForFixedUpdate waiter = new WaitForFixedUpdate();
		float timer = 0f;
		float duration = 0.5f + (Random.value * 2);
		while(timer < duration) {
			timer += Time.deltaTime;
			
			transform.rotation = Quaternion.Lerp(startQuaternion, endQuaternion, timer/duration);

			yield return waiter;
		}
	}

	public void StartWatching() {
		if(focusCoroutine != null) {
			StopCoroutine(focusCoroutine);
		}

		focusCoroutine = StartCoroutine(AdjustFocus());
	}

	public void StopWatching() {
		if(focusCoroutine != null) {
			StopCoroutine(focusCoroutine);
			focusCoroutine = null;
		}
	}
	
	public IEnumerator AdjustFocus() {
		WaitForFixedUpdate waiter = new WaitForFixedUpdate();

		int frameInterval = 2;
		while(true) {

			if(Time.frameCount % frameInterval == 0) {
				if(focalObject == null) {
					StopWatching();
				} else {
					transform.rotation = GetFocalQuaternion();
				}
			}

			yield return waiter;
		}
	}

	public Quaternion GetFocalQuaternion() {
		Vector3 offset = transform.position - focalObject.transform.position;
		Quaternion newRotation = Quaternion.LookRotation(Vector3.forward, offset);
		//newRotation.eulerAngles = new Vector3(newRotation.x, newRotation.y, Mathf.Clamp(newRotation.eulerAngles.z, -30, 30));

		return newRotation;
	}

	public IEnumerator RemoveAthleteFromField(Vector3 goalCenter) {
        //Fade the athlete out first - similar to pokeball effect

		StoppedMoving();
		body.DisableBody();

		Vector3 originalPosition = transform.position;
		Vector3 absorbPosition = goalCenter;
		absorbPosition.y = originalPosition.y;

		yield return new WaitForSeconds(0.2f);

		//SetAllSpritesWhite();

		WaitForFixedUpdate waiter = new WaitForFixedUpdate();
		float duration = 1f;
		float timer = 0f;
		while(timer < duration) {
			timer += Time.deltaTime;

			transform.position = Vector3.Lerp(originalPosition, absorbPosition, timer/duration);

			transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, timer/duration);

			yield return waiter;
		}

        gameObject.SetActive(false);
        gameObject.transform.position = Vector2.zero;
    }

	public void SetAllSpritesWhite() {
		/*
		Shader shade = Resources.Load("Shaders/Unlit_WhiteShader", typeof(Shader)) as Shader;
		jersey.material.shader = shade;
		body.SetMaterial(shade);
		face.SetMaterial(shade);
		tailTip.SetMaterial(shade);
		*/
		
		jersey.color = Color.white;
		body.SetColor(Color.white);
		face.SetColor(Color.white);
		tailTip.SetColor(Color.white);
		
		for(int i = 0; i < legList.Count; i++) {
			legList[i].color = Color.white;
		}
	}

	public void SubstituteAthleteOnField(Vector2 spawnPosition, Vector3 spawnAngle, Transform chair) {
		gameObject.SetActive(true);
		transform.localPosition = spawnPosition;
		transform.eulerAngles = spawnAngle;
		transform.localScale = originalScale;

		substitute = true;
		gameObject.layer = 8;

		body.EnableBody();

		//RestoreAthleteColor();

		DisableInteraction();

		//StartCoroutine(LaunchAthleteToSubstituteChair(0.43f, chair));
		AttachToChair(chair);
	}

	public IEnumerator LaunchAthleteToSubstituteChair(float percentForce, Transform chair) {
		directionDragged = Vector3.zero - transform.position;
		if(directionDragged.magnitude >= athlete.maxPull) {
			directionDragged = Vector3.ClampMagnitude(directionDragged, athlete.maxPull);
		} //The direction starts maximimally dragged towards the center

		directionDragged *= percentForce; //And then is multiplied by percentForce. 0.5f should result in half max force, etc... (But is there a min force? I don't think so bro we good.)

		Vector2 minDirectionDrag = directionDragged.normalized * athlete.minPull;
		Vector2 adjustedDirection = (Vector2)directionDragged - minDirectionDrag;
		if(adjustedDirection.magnitude < 0) {
			adjustedDirection = Vector2.zero;
		}

		Vector2 force = adjustedDirection * athlete.flingForce;
		body.AddForce(force);

		directionDragged = Vector3.zero;

		ResetTail();

		WaitForFixedUpdate waiter = new WaitForFixedUpdate();
		moving = true;
		while(moving) {
			yield return waiter;
		}

		AttachToChair(chair);
	}

	public void AttachToChair(Transform chair) {
		transform.SetParent(chair);
		transform.localPosition = Vector2.zero;

		matchController.StartCoroutine(matchController.MoveChairOntoField(chair));
	}

	public void EnteredBumper(Bumper bumper) {
		if(substitute) {
			bumpersInside.Add(bumper);
		}
	}

	public void ExitedBumper(Bumper bumper) {
		if(substitute) {
			bumpersInside.Remove(bumper);

			/*
			if(bumpersInside.Count == 0) {
				matchController.AddAthleteToField(this);
			}
			*/
		}
	}
}
