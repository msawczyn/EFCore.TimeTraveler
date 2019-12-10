﻿using System;
using System.Collections.Generic;

namespace EFCore.TimeTravelerTests
{
    public class Apple
    {
        public Guid Id { get; set; }
        public FruitStatus FruitStatus { get; set; }

        public List<Worm> Worms { get; set; } = new List<Worm>();
    }
}
