﻿using Idasen.BluetoothLE.Core.Tests.DevicesDiscovery.ConstructorNullTesters ;
using Idasen.BluetoothLE.Linak.Control ;
using Microsoft.VisualStudio.TestTools.UnitTesting ;

namespace Idasen.BluetoothLE.Linak.Tests.ConstructorNullTester
{
    [ TestClass ]
    public class DeskMoverConstructorNullTests
        : BaseConstructorNullTester < DeskMover >
    {
        public override int NumberOfConstructorsPassed { get ; } = 1 ;
    }
}