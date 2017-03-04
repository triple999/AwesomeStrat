﻿using Assets.Map;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class IntChangedEvent : UnityEvent<int> { }

public class MapUnit : Unit
{
    bool hasTakenAction;
}
