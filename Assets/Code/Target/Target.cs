﻿using ScriptableObjectArchitecture;
using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class Target : MonoBehaviour
{
    [SerializeField] private TargetRender targetRender;

    [SerializeField] private TargetAudio targetAudio;

    [SerializeField] private ContactFilter2D contactFilter2D;

    [SerializeField] private IntGameEvent playerCollidedWithTargetEvent;

    [SerializeField] private float secondsBetweenCollisions = 1f;

    private int _index;
    private int _totalNumForLevel;
    private LevelData _ld;
    private bool _destroyBecauseOfDeath;

    public enum TargetState
    {
        Initiating,
        Idle,
        Colliding,
        CollideFinished,
        Dying
    }

    
    public enum TargetStateRedux
    {
        Tweening,
        Awaiting, //waiting to get hit in correct order
        Correct,
        Wrong,
        Dying
    }

    private TargetState curState;

    private TargetStateRedux curStateRedux;
    private bool _shouldCheckCollisions;
    private Collider2D _collider2D;
    private List<Collider2D> _collisionBuffer;

    private void Awake()
    {
        _collider2D = GetComponent<Collider2D>();

        _collisionBuffer = new List<Collider2D>();
    }

    public void Construct(int index, int totalNumForLevel, LevelData ld)
    {
        // ld is for play mode saving only in edit mode
        _index = index;

        _totalNumForLevel = totalNumForLevel;

        _ld = ld;

        targetRender.ConstructAndAnimate(index);

        targetAudio.OnPlayerHit(_index, _totalNumForLevel, hitCorrectly: true);

        // only so we can edit stuff in edit mode
        _destroyBecauseOfDeath = false;

        _shouldCheckCollisions = false;

        curState = TargetState.Idle;

        curStateRedux = TargetStateRedux.Awaiting;

    }

    private void Update()
    {
        /*
        if (curState == TargetState.Idle)
        {
            CheckForPlayerCollision();
        }*/

        /*
        if (curStateRedux == TargetStateRedux.Awaiting)
        {
            CheckForPlayerCollision();
        }*/

        if (_shouldCheckCollisions)
        {
            CheckForPlayerCollision();
        }
    }

    private void CheckForPlayerCollision()
    {
        var numCollisions = _collider2D.OverlapCollider(contactFilter2D, _collisionBuffer);

        if (numCollisions > 0)
        {
            CollisionFlow();
        }
    }

    private void CollisionFlow()
    {
        _shouldCheckCollisions = false;

        //targetAudio.OnPlayerHit(_index, _totalNumForLevel);

        playerCollidedWithTargetEvent.Raise(_index);

        //curState = TargetState.CollideFinished;
    }

    public void BeginDeathSentence()
    {
        _destroyBecauseOfDeath = true;
        _shouldCheckCollisions = false;

        //shrink!!
        targetRender.BeginShrink();

        StartCoroutine(WaitForStuffThenDie());
    }

    private IEnumerator WaitForStuffThenDie()
    {
        // hmm not robust
        _shouldCheckCollisions = false;

        yield return new WaitWhile(
            () => targetAudio.currentlyPlaying && targetRender.CurrentlyShrinking);

        Destroy(this.gameObject);
    }

    public void OnLevelCompleted()
    {
        _shouldCheckCollisions = false;
    }

    public void OnPlayerLoopedIncomplete()
    {
        targetRender.SetToDefault();
        curStateRedux = TargetStateRedux.Awaiting; 



        /*
        if (curState == TargetState.Colliding ||
            curState == TargetState.CollideFinished)
        {
            curState = TargetState.Idle;
        }
        */
    }

    public void OnDefaultState()
    {
        if (curStateRedux != TargetStateRedux.Awaiting)
        {
            targetRender.SetToDefault();

            _shouldCheckCollisions = true;

            curStateRedux = TargetStateRedux.Awaiting;
        }
    }

    public void OnCorrectlyHit(bool playAudio = true)
    {
        targetRender.SetToCorrectOrder();

        if (playAudio)
        {
            targetAudio.OnPlayerHit(_index, _totalNumForLevel, hitCorrectly: true);
        }

        curStateRedux = TargetStateRedux.Correct;


        _shouldCheckCollisions = false;
        StartCoroutine(ChillThen(secondsBetweenCollisions, TurnCollisionCheckOnIfNotDead));

        /*
        if (curState == TargetState.Colliding ||
            curState == TargetState.CollideFinished)
        {
            curState = TargetState.Idle;
        }*/
    }

    public void OnIncorrectlyHit()
    {
        targetRender.SetToWrongOrder();

        targetAudio.OnPlayerHit(_index, _totalNumForLevel, hitCorrectly: false);

        curStateRedux = TargetStateRedux.Wrong;

        _shouldCheckCollisions = false;
        StartCoroutine(ChillThen(secondsBetweenCollisions, TurnCollisionCheckOnIfNotDead));

    }
    private IEnumerator ChillThen(float secs, Action a)
    {
        yield return new WaitForSeconds(secs);
        a.Invoke();
    }

    public void TurnCollisionCheckOnIfNotDead()
    {
        if (!_destroyBecauseOfDeath)
        {
            _shouldCheckCollisions = true;
        }
    }

#if UNITY_EDITOR

    private void OnDestroy()
    {
        TryOverwriteLevelData();
    }

    private void TryOverwriteLevelData()
    {
        if (Application.isEditor && Application.isPlaying && !_destroyBecauseOfDeath
            && _ld != null)
        {
            _ld.TargetPositions[_index] = transform.position;

            EditorUtility.SetDirty(_ld);
        }
    }

#endif
}
