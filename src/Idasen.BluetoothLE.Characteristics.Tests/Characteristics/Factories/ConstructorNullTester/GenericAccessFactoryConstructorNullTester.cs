﻿using Idasen.BluetoothLE.Characteristics.Characteristics.Factories ;
using Idasen.BluetoothLE.Core.Tests.DevicesDiscovery.ConstructorNullTesters ;
using Microsoft.VisualStudio.TestTools.UnitTesting ;

namespace Idasen.BluetoothLE.Characteristics.Tests.Characteristics.Factories.ConstructorNullTester
{
    [ TestClass ]
    public class GenericAccessFactoryConstructorNullTester
        : BaseConstructorNullTester < GenericAccessFactory >
    {
        public override int NumberOfConstructorsPassed { get ; } = 1 ;
    }
}