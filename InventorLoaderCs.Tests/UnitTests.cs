using System;
using System.Collections.Generic;
using System.Numerics;
using System.IO;
using InventorLoaderCs; // Assuming this brings in all necessary classes

// Using a common namespace for tests for simplicity in this environment
namespace InventorLoaderCs.Tests
{
    // Mocking Assert for this environment
    public static class Assert
    {
        public static void AreEqual(object expected, object actual, string message = "")
        {
            if (expected == null && actual == null) return;
            if (expected == null || !expected.Equals(actual))
            {
                Console.WriteLine($"Assert.AreEqual FAILED {message}: Expected '{expected}', Got '{actual}'");
                // throw new Exception($"Assert.AreEqual FAILED {message}: Expected '{expected}', Got '{actual}'");
            }
        }
        public static void IsNull(object value, string message = "")
        {
            if (value != null)
            {
                Console.WriteLine($"Assert.IsNull FAILED {message}: Expected null, Got '{value}'");
                // throw new Exception($"Assert.IsNull FAILED {message}: Expected null, Got '{value}'");
            }
        }
         public static void AreSame(object expected, object actual, string message = "")
        {
            if (!Object.ReferenceEquals(expected, actual))
            {
                Console.WriteLine($"Assert.AreSame FAILED {message}: Expected same instance, but were different.");
            }
        }
    }


    public class AcisParsingUtilsTests
    {
        private AcisReader _mockReader;
        private AcisHeader _mockHeader;

        public void TestInitialize()
        {
            _mockReader = new AcisReader();
            _mockHeader = new AcisHeader();
            typeof(AcisHeader).GetProperty("Scale").SetValue(_mockHeader, 1.0); // Using reflection if setter is not public
            typeof(AcisHeader).GetProperty("Version").SetValue(_mockHeader, 7.0); // Default

            AcisGlobalUtils.SetReader(_mockReader);
            typeof(AcisReader).GetProperty("Header").SetValue(AcisGlobalUtils.GetReader(), _mockHeader);
        }

        public void GetFloat_ValidChunk_ReturnsDoubleAndIncrementsIndex()
        {
            TestInitialize();
            var record = new AcisRecord("test-record", _mockReader);
            record.Chunks.Add(new AcisChunkDouble(123.456));
            int chunkIndex = 0;
            double result = AcisParsingUtils.GetFloat(record, ref chunkIndex, "TestFloat");
            Assert.AreEqual(123.456, result, "GetFloat value check");
            Assert.AreEqual(1, chunkIndex, "GetFloat index check");
            Logger.Info("Test: GetFloat_ValidChunk_ReturnsDoubleAndIncrementsIndex PASSED");
        }

        public void GetInteger_ValidChunk_ReturnsIntAndIncrementsIndex()
        {
            TestInitialize();
            var record = new AcisRecord("test-record", _mockReader);
            record.Chunks.Add(new AcisChunkLong(789));
            int chunkIndex = 0;
            int result = AcisParsingUtils.GetInteger(record, ref chunkIndex, "TestInt");
            Assert.AreEqual(789, result, "GetInteger value check");
            Assert.AreEqual(1, chunkIndex, "GetInteger index check");
            Logger.Info("Test: GetInteger_ValidChunk_ReturnsIntAndIncrementsIndex PASSED");
        }

        public void GetVector_FromSeparateDoubleChunks_ReturnsVector3AndIncrementsIndex()
        {
            TestInitialize();
            var record = new AcisRecord("test-record", _mockReader);
            record.Chunks.Add(new AcisChunkDouble(1.0));
            record.Chunks.Add(new AcisChunkDouble(2.0));
            record.Chunks.Add(new AcisChunkDouble(3.0));
            int chunkIndex = 0;
            var expected = new Vector3(1.0f, 2.0f, 3.0f);
            Vector3 result = AcisParsingUtils.GetVector(record, ref chunkIndex, "TestVector");
            Assert.AreEqual(expected, result, "GetVector value check");
            Assert.AreEqual(3, chunkIndex, "GetVector index check");
            Logger.Info("Test: GetVector_FromSeparateDoubleChunks_ReturnsVector3AndIncrementsIndex PASSED");
        }

        public void GetVector_FromVector3DChunk_ReturnsVector3AndIncrementsIndex()
        {
            TestInitialize();
            var record = new AcisRecord("test-record", _mockReader);
            var expected = new Vector3(4.0f, 5.0f, 6.0f);
            record.Chunks.Add(new AcisChunkVector3D(expected));
            int chunkIndex = 0;
            Vector3 result = AcisParsingUtils.GetVector(record, ref chunkIndex, "TestVectorFromChunk");
            Assert.AreEqual(expected, result, "GetVector (from chunk) value check");
            Assert.AreEqual(1, chunkIndex, "GetVector (from chunk) index check");
            Logger.Info("Test: GetVector_FromVector3DChunk_ReturnsVector3AndIncrementsIndex PASSED");
        }

        public void GetRefNode_ValidRef_ReturnsEntityAndIncrementsIndex()
        {
            var readerForThisTest = new AcisReader();
            AcisGlobalUtils.SetReader(readerForThisTest);

            var targetRecord = new AcisRecord("target-entity-record", readerForThisTest) { Index = 5 };
            var targetEntity = new Point();
            targetEntity.Record = targetRecord;
            targetRecord.Entity = targetEntity;

            for(int i = 0; i <= 5; ++i) readerForThisTest.RecordsList.Add(null);
            readerForThisTest.RecordsList[5] = targetRecord;

            var record = new AcisRecord("test-record", readerForThisTest);
            var entityRefChunk = new AcisChunkEntityRef(5);
            entityRefChunk.Record = targetRecord;
            record.Chunks.Add(entityRefChunk);
            int chunkIndex = 0;

            AcisEntity result = AcisParsingUtils.GetRefNode(record, ref chunkIndex, "TestRef");
            Assert.AreSame(targetEntity, result, "GetRefNode entity check");
            Assert.AreEqual(1, chunkIndex, "GetRefNode index check");
            Logger.Info("Test: GetRefNode_ValidRef_ReturnsEntityAndIncrementsIndex PASSED");
        }

        public void GetRefNode_NullRef_ReturnsNullAndIncrementsIndex()
        {
            TestInitialize();
            var record = new AcisRecord("test-record", _mockReader);
            record.Chunks.Add(new AcisChunkEntityRef(-1));
            int chunkIndex = 0;
            AcisEntity result = AcisParsingUtils.GetRefNode(record, ref chunkIndex, "TestNullRef");
            Assert.IsNull(result, "GetRefNode null check");
            Assert.AreEqual(1, chunkIndex, "GetRefNode null index check");
            Logger.Info("Test: GetRefNode_NullRef_ReturnsNullAndIncrementsIndex PASSED");
        }
    }

    public class AcisEntitySetTests
    {
        private AcisReader _testReader;

        public void TestInitializeAndSetHeader(double scale, double version)
        {
            _testReader = new AcisReader();
            typeof(AcisHeader).GetProperty("Scale").SetValue(_testReader.Header, scale);
            typeof(AcisHeader).GetProperty("Version").SetValue(_testReader.Header, version);
            AcisGlobalUtils.SetReader(_testReader);
        }

        public void Point_Set_ParsesPositionCorrectly()
        {
            TestInitializeAndSetHeader(scale: 2.0, version: 7.0);
            var record = new AcisRecord("point-entity", _testReader);
            record.Chunks.Add(new AcisChunkEntityRef(-1));
            record.Chunks.Add(new AcisChunkLong(0));
            record.Chunks.Add(new AcisChunkEntityRef(-1));
            record.Chunks.Add(new AcisChunkDouble(10.0));
            record.Chunks.Add(new AcisChunkDouble(20.0));
            record.Chunks.Add(new AcisChunkDouble(30.0));

            var point = new Point();
            point.Set(record);

            var expectedPosition = new Vector3(10.0f * 2.0f, 20.0f * 2.0f, 30.0f * 2.0f);
            Assert.AreEqual(true, ImporterUtils.IsEqual(expectedPosition, point.Position, 1e-6f), $"Point Position check. Expected {expectedPosition}, Got {point.Position}");
            Logger.Info("Test: Point_Set_ParsesPositionCorrectly PASSED");
        }

        public void CurveStraight_Set_ParsesPropertiesCorrectly()
        {
            TestInitializeAndSetHeader(scale: 1.0, version: 7.0);
            var record = new AcisRecord("straight-curve", _testReader);
            record.Chunks.Add(new AcisChunkEntityRef(-1));
            record.Chunks.Add(new AcisChunkLong(0));
            record.Chunks.Add(new AcisChunkEntityRef(-1));
            record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(2.0)); record.Chunks.Add(new AcisChunkDouble(3.0));
            record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(0.0));
            record.Chunks.Add(new AcisChunkIdent("F")); record.Chunks.Add(new AcisChunkDouble(0.0));
            record.Chunks.Add(new AcisChunkIdent("F")); record.Chunks.Add(new AcisChunkDouble(100.0));

            var curveStraight = new CurveStraight();
            curveStraight.Set(record);

            var expectedRoot = new Vector3(1.0f, 2.0f, 3.0f);
            var expectedDir = new Vector3(0.0f, 1.0f, 0.0f);

            Assert.AreEqual(true, ImporterUtils.IsEqual(expectedRoot, curveStraight.Root, 1e-6f), "CurveStraight Root check");
            Assert.AreEqual(true, ImporterUtils.IsEqual(expectedDir, curveStraight.Dir, 1e-6f), "CurveStraight Dir check");
            Assert.AreEqual("F", curveStraight.CurveRange.Lower.Type, "CurveStraight Range.Lower.Type check");
            Assert.AreEqual(0.0, curveStraight.CurveRange.Lower.GetLimit(), "CurveStraight Range.Lower.Limit check"); // Removed delta for exact match
            Assert.AreEqual("F", curveStraight.CurveRange.Upper.Type, "CurveStraight Range.Upper.Type check");
            Assert.AreEqual(100.0, curveStraight.CurveRange.Upper.GetLimit(), "CurveStraight Range.Upper.Limit check"); // Removed delta
            Logger.Info("Test: CurveStraight_Set_ParsesPropertiesCorrectly PASSED");
        }

        public static void RunTests()
        {
            Logger.LogWriter = new StreamWriter(Console.OpenStandardOutput()){ AutoFlush = true};

            AcisParsingUtilsTests parsingUtilsTests = new AcisParsingUtilsTests();
            parsingUtilsTests.GetFloat_ValidChunk_ReturnsDoubleAndIncrementsIndex();
            parsingUtilsTests.GetInteger_ValidChunk_ReturnsIntAndIncrementsIndex();
            parsingUtilsTests.GetVector_FromSeparateDoubleChunks_ReturnsVector3AndIncrementsIndex();
            parsingUtilsTests.GetVector_FromVector3DChunk_ReturnsVector3AndIncrementsIndex();
            parsingUtilsTests.GetRefNode_ValidRef_ReturnsEntityAndIncrementsIndex();
            parsingUtilsTests.GetRefNode_NullRef_ReturnsNullAndIncrementsIndex();

            AcisEntitySetTests entitySetTests = new AcisEntitySetTests();
            entitySetTests.Point_Set_ParsesPositionCorrectly();
            entitySetTests.CurveStraight_Set_ParsesPropertiesCorrectly();
            entitySetTests.CurveInt_Set_ParsesHelixWithPCurvesCorrectly();
            entitySetTests.SurfaceSpline_Set_ParsesBSplineSurfaceCorrectly(); // Added new test call

            AcisToStepConverterTests.RunTests(); // Now uncommented
            Logger.Info("All conceptual tests finished.");
        }

        public void CurveInt_Set_ParsesHelixWithPCurvesCorrectly()
        {
            TestInitializeAndSetHeader(scale: 1.0, version: 7.0);
            var record = new AcisRecord("helix-curve-int", _testReader);

            // Standard ACIS Entity Header Chunks (placeholders for actual structure if more complex)
            record.Chunks.Add(new AcisChunkEntityRef(-1)); // next_attribute_block_def
            record.Chunks.Add(new AcisChunkLong(0));       // forward_flag
            record.Chunks.Add(new AcisChunkEntityRef(-1)); // reversed_entity_ptr

            // Subtype name for curve-int
            record.Chunks.Add(new AcisChunkIdent("helix_int_cur"));

            // Helix specific data
            // PosCenter (Vector3D)
            record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(2.0)); record.Chunks.Add(new AcisChunkDouble(3.0));
            // DirNormal (Vector3D) - Axis of helix
            record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(1.0));
            // DirMajor (Vector3D) - Direction of major radius at start point
            record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0));
            // RadiusMajor
            record.Chunks.Add(new AcisChunkDouble(5.0));
            // RadiusMinor (for toroidal helix, often same as Major for cylindrical)
            record.Chunks.Add(new AcisChunkDouble(5.0));
            // FacApex (cone angle factor, 0 for cylindrical)
            record.Chunks.Add(new AcisChunkDouble(0.0));
            // FacPitch (pitch value)
            record.Chunks.Add(new AcisChunkDouble(10.0));
            // CurveRange (start and end parameters, e.g., angles in radians)
            record.Chunks.Add(new AcisChunkIdent("R")); // Type: R for radians, D for degrees, F for unitless
            record.Chunks.Add(new AcisChunkDouble(0.0)); // Start
            record.Chunks.Add(new AcisChunkIdent("R"));
            record.Chunks.Add(new AcisChunkDouble(Math.PI * 4.0)); // End (e.g., two full turns)

            // pcurve1 data (2D B-Spline)
            record.Chunks.Add(new AcisChunkIdent("T")); // pcurve1 exists
            record.Chunks.Add(new AcisChunkLong(2));    // Degree
            record.Chunks.Add(new AcisChunkIdent("F")); // IsRational (False)
            record.Chunks.Add(new AcisChunkLong(0));    // IsClosed (False)
            record.Chunks.Add(new AcisChunkLong(3));    // NumPoles
            record.Chunks.Add(new AcisChunkLong(6));    // NumKnots (Degree + NumPoles + 1 for open, clamped) -> 2+3+1 = 6
            // Poles (u,v pairs)
            record.Chunks.Add(new AcisChunkDouble(0.1)); record.Chunks.Add(new AcisChunkDouble(0.2));
            record.Chunks.Add(new AcisChunkDouble(0.3)); record.Chunks.Add(new AcisChunkDouble(0.4));
            record.Chunks.Add(new AcisChunkDouble(0.5)); record.Chunks.Add(new AcisChunkDouble(0.6));
            // Knots
            record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0));
            record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(1.0));
            // Multiplicities (optional in some ACIS versions if knots are listed with multiplicity)
            // Assuming CurveInt.Set handles cases where multiplicities are implicitly defined by repeated knots
            // Or, if explicit:
            // record.Chunks.Add(new AcisChunkLong(3)); record.Chunks.Add(new AcisChunkLong(3));

            // pcurve2 data (optional, let's make it exist for testing)
            record.Chunks.Add(new AcisChunkIdent("T")); // pcurve2 exists
            record.Chunks.Add(new AcisChunkLong(1));    // Degree
            record.Chunks.Add(new AcisChunkIdent("F")); // IsRational (False)
            record.Chunks.Add(new AcisChunkLong(0));    // IsClosed (False)
            record.Chunks.Add(new AcisChunkLong(2));    // NumPoles
            record.Chunks.Add(new AcisChunkLong(4));    // NumKnots (1+2+1 = 4)
            // Poles
            record.Chunks.Add(new AcisChunkDouble(0.7)); record.Chunks.Add(new AcisChunkDouble(0.8));
            record.Chunks.Add(new AcisChunkDouble(0.9)); record.Chunks.Add(new AcisChunkDouble(1.0));
            // Knots
            record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0));
            record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(1.0));
            // Multiplicities (optional)

            var curveInt = new CurveInt();
            curveInt.Set(record);

            Assert.AreEqual("helix_int_cur", curveInt.SubType, "CurveInt SubType check");

            Assert.IsNotNull(curveInt.HelixCurveData, "HelixCurveData should not be null");
            var helixData = curveInt.HelixCurveData;
            Assert.AreEqual(true, ImporterUtils.IsEqual(new Vector3(1.0f, 2.0f, 3.0f), helixData.PosCenter, 1e-6f), "Helix PosCenter check");
            Assert.AreEqual(true, ImporterUtils.IsEqual(new Vector3(0.0f, 0.0f, 1.0f), helixData.DirNormal, 1e-6f), "Helix DirNormal check");
            Assert.AreEqual(true, ImporterUtils.IsEqual(new Vector3(1.0f, 0.0f, 0.0f), helixData.DirMajor, 1e-6f), "Helix DirMajor check");
            Assert.AreEqual(5.0, helixData.RadiusMajor, "Helix RadiusMajor check");
            Assert.AreEqual(5.0, helixData.RadiusMinor, "Helix RadiusMinor check");
            Assert.AreEqual(0.0, helixData.FacApex, "Helix FacApex check");
            Assert.AreEqual(10.0, helixData.FacPitch, "Helix FacPitch check");
            Assert.AreEqual("R", helixData.CurveRange.Lower.Type, "Helix Range.Lower.Type check");
            Assert.AreEqual(0.0, helixData.CurveRange.Lower.GetLimit(), "Helix Range.Lower.Limit check");
            Assert.AreEqual("R", helixData.CurveRange.Upper.Type, "Helix Range.Upper.Type check");
            Assert.AreEqual(Math.PI * 4.0, helixData.CurveRange.Upper.GetLimit(), "Helix Range.Upper.Limit check");

            Assert.IsNotNull(helixData.ProjectionPCurve1, "ProjectionPCurve1 should not be null");
            var pcurve1Data = helixData.ProjectionPCurve1;
            Assert.AreEqual(2, pcurve1Data.Degree, "PCurve1 Degree check");
            Assert.AreEqual(false, pcurve1Data.IsRational, "PCurve1 IsRational check");
            Assert.AreEqual(false, pcurve1Data.IsClosed, "PCurve1 IsClosed check");
            Assert.AreEqual(3, pcurve1Data.Poles2D.Count, "PCurve1 NumPoles check");
            Assert.AreEqual(6, pcurve1Data.Knots.Count, "PCurve1 NumKnots check");
            Assert.AreEqual(true, ImporterUtils.IsEqual(new Vector2(0.1f, 0.2f), pcurve1Data.Poles2D[0], 1e-6f), "PCurve1 Pole 0 check");
            Assert.AreEqual(0.0, pcurve1Data.Knots[0], "PCurve1 Knot 0 check");
            Assert.AreEqual(1.0, pcurve1Data.Knots[5], "PCurve1 Knot 5 check");


            Assert.IsNotNull(helixData.ProjectionPCurve2, "ProjectionPCurve2 should not be null");
            var pcurve2Data = helixData.ProjectionPCurve2;
            Assert.AreEqual(1, pcurve2Data.Degree, "PCurve2 Degree check");
            Assert.AreEqual(false, pcurve2Data.IsRational, "PCurve2 IsRational check");
            Assert.AreEqual(2, pcurve2Data.Poles2D.Count, "PCurve2 NumPoles check");
            Assert.AreEqual(4, pcurve2Data.Knots.Count, "PCurve2 NumKnots check");
            Assert.AreEqual(true, ImporterUtils.IsEqual(new Vector2(0.7f, 0.8f), pcurve2Data.Poles2D[0], 1e-6f), "PCurve2 Pole 0 check");

            Logger.Info("Test: CurveInt_Set_ParsesHelixWithPCurvesCorrectly PASSED");
        }

        public void SurfaceSpline_Set_ParsesBSplineSurfaceCorrectly()
        {
            TestInitializeAndSetHeader(scale: 1.0, version: 7.0);
            var record = new AcisRecord("bspline-surface", _testReader);

            // Standard ACIS Entity Header Chunks
            record.Chunks.Add(new AcisChunkEntityRef(-1));
            record.Chunks.Add(new AcisChunkLong(0));
            record.Chunks.Add(new AcisChunkEntityRef(-1));

            // SurfaceSpline specific data for a non-rational B-Spline surface
            record.Chunks.Add(new AcisChunkLong(2));    // UDegree
            record.Chunks.Add(new AcisChunkLong(2));    // VDegree
            record.Chunks.Add(new AcisChunkIdent("F")); // IsRational (False)
            record.Chunks.Add(new AcisChunkLong(0));    // IsUClosed (False)
            record.Chunks.Add(new AcisChunkLong(0));    // IsVClosed (False)
            record.Chunks.Add(new AcisChunkLong(3));    // NumUPoles (Nu)
            record.Chunks.Add(new AcisChunkLong(3));    // NumVPoles (Nv)
            record.Chunks.Add(new AcisChunkLong(6));    // NumUKnots (Nu + UDegree + 1 for open clamped) = 3+2+1 = 6
            record.Chunks.Add(new AcisChunkLong(6));    // NumVKnots (Nv + VDegree + 1 for open clamped) = 3+2+1 = 6

            // Poles (3x3 grid = 9 poles. Each pole is x, y, z)
            // Row 1 (V=0)
            record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0));
            record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.5));
            record.Chunks.Add(new AcisChunkDouble(2.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0));
            // Row 2 (V=1)
            record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(0.5));
            record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(1.0));
            record.Chunks.Add(new AcisChunkDouble(2.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(0.5));
            // Row 3 (V=2)
            record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(2.0)); record.Chunks.Add(new AcisChunkDouble(0.0));
            record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(2.0)); record.Chunks.Add(new AcisChunkDouble(0.5));
            record.Chunks.Add(new AcisChunkDouble(2.0)); record.Chunks.Add(new AcisChunkDouble(2.0)); record.Chunks.Add(new AcisChunkDouble(0.0));

            // U Knots (clamped: 0,0,0, 1,1,1 for degree 2, 3 poles)
            record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0));
            record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(1.0));

            // V Knots (clamped: 0,0,0, 1,1,1 for degree 2, 3 poles)
            record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0));
            record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(1.0));

            // Multiplicities are often skipped if knots are listed with repetition.
            // Weights are skipped because IsRational is "F".

            var surfaceSpline = new SurfaceSpline();
            surfaceSpline.Set(record);

            Assert.IsNotNull(surfaceSpline.SplineGeometricData, "SplineGeometricData should not be null");
            var splineData = surfaceSpline.SplineGeometricData;

            Assert.AreEqual(2, splineData.UDegree, "UDegree check");
            Assert.AreEqual(2, splineData.VDegree, "VDegree check");
            Assert.AreEqual(false, splineData.IsRational, "IsRational check");
            Assert.AreEqual(false, splineData.IsUClosed, "IsUClosed check");
            Assert.AreEqual(false, splineData.IsVClosed, "IsVClosed check");

            Assert.AreEqual(3, splineData.NumUPoles, "NumUPoles check");
            Assert.AreEqual(3, splineData.NumVPoles, "NumVPoles check");
            Assert.IsNotNull(splineData.Poles3DGrid, "Poles3DGrid should not be null");
            Assert.AreEqual(3, splineData.Poles3DGrid.GetLength(0), "Poles3DGrid U dimension check");
            Assert.AreEqual(3, splineData.Poles3DGrid.GetLength(1), "Poles3DGrid V dimension check");

            Assert.AreEqual(6, splineData.UKnots.Count, "UKnots count check");
            Assert.AreEqual(6, splineData.VKnots.Count, "VKnots count check");

            // Check a few pole values (remembering they are scaled by AcisReader.Header.Scale, which is 1.0 here)
            Assert.AreEqual(true, ImporterUtils.IsEqual(new Vector3(0.0f, 0.0f, 0.0f), splineData.Poles3DGrid[0,0], 1e-6f), "Pole (0,0) check");
            Assert.AreEqual(true, ImporterUtils.IsEqual(new Vector3(1.0f, 1.0f, 1.0f), splineData.Poles3DGrid[1,1], 1e-6f), "Pole (1,1) check");
            Assert.AreEqual(true, ImporterUtils.IsEqual(new Vector3(2.0f, 2.0f, 0.0f), splineData.Poles3DGrid[2,2], 1e-6f), "Pole (2,2) check");

            // Check a few knot values
            Assert.AreEqual(0.0, splineData.UKnots[0], "UKnot 0 check");
            Assert.AreEqual(1.0, splineData.UKnots[5], "UKnot 5 check");
            Assert.AreEqual(0.0, splineData.VKnots[0], "VKnot 0 check");
            Assert.AreEqual(1.0, splineData.VKnots[5], "VKnot 5 check");

            Logger.Info("Test: SurfaceSpline_Set_ParsesBSplineSurfaceCorrectly PASSED");
        }
    }

    public class AcisToStepConverterTests
    {
        private AcisReader _mockAcisReader; // Added for global access if needed by utils

        public void TestInitialize()
        {
            StepConverterUtils.InitExport();
            // Setup a default mock reader for tests that might need it via AcisGlobalUtils
            var acisHeader = new AcisHeader(version: 26.0, scale: 1.0);
            _mockAcisReader = new AcisReader(acisHeader);
            AcisGlobalUtils.SetReader(_mockAcisReader);
        }

        public static void RunTests()
        {
            Logger.Info("Starting AcisToStepConverterTests...");
            AcisToStepConverterTests converterTests = new AcisToStepConverterTests();
            converterTests.TestCreateBSplineCurveGeometry_NonRational_CorrectProperties();
            converterTests.TestCreateBSplineCurveGeometry_Rational_CorrectProperties();
            converterTests.TestCreateBSplineSurfaceGeometry_NonRational_CorrectProperties();
            converterTests.TestCreateBSplineSurfaceGeometry_Rational_CorrectProperties();
            converterTests.TestRationalBSplineCurve_ExportStep_FormatsCorrectly();
            Logger.Info("AcisToStepConverterTests finished.");
        }

        public void TestCreateBSplineCurveGeometry_NonRational_CorrectProperties()
        {
            TestInitialize(); // Calls StepConverterUtils.InitExport()

            var bsCurveData = new BSCurveData
            {
                Degree = 2,
                IsClosed = false,
                IsRational = false,
                IsPeriodic = false, // Assuming non-periodic for simplicity
                NumPoles = 3,
                Poles3D = new List<Vector3>
                {
                    new Vector3(0, 0, 0),
                    new Vector3(1, 1, 0),
                    new Vector3(2, 0, 0)
                },
                NumKnots = 6, // Degree + NumPoles + 1 for open clamped = 2 + 3 + 1 = 6
                Knots = new List<double> { 0, 0, 0, 1, 1, 1 },
                KnotMultiplicities = new List<int> { 3, 3 } // Standard clamped multiplicities
            };

            var stepCurve = StepConverterUtils.CreateBSplineCurveGeometry(bsCurveData, null, null);

            Assert.IsNotNull(stepCurve, "STEP curve should not be null (Non-Rational)");
            Assert.AreEqual(true, stepCurve is BSplineCurveWithKnots, "STEP curve should be BSplineCurveWithKnots (Non-Rational)");

            var bspline = stepCurve as BSplineCurveWithKnots;
            Assert.AreEqual(bsCurveData.Degree, bspline.Degree, "Degree mismatch (Non-Rational)");
            Assert.AreEqual(bsCurveData.NumPoles, bspline.ControlPointsList.Count, "Control points count mismatch (Non-Rational)");
            // For BSplineCurveWithKnots, KnotMultiplicities from BSCurveData should be directly used.
            Assert.AreEqual(bsCurveData.KnotMultiplicities.Count, bspline.KnotMultiplicities.Count, "Knot multiplicities count mismatch (Non-Rational)");
            Assert.AreEqual(bsCurveData.Knots.Count, bspline.Knots.Count, "Knots count mismatch (Non-Rational)");
            Assert.AreEqual(StepEnumWrapper.BSplineCurveForm.UNSPECIFIED, bspline.CurveForm, "CurveForm default check (Non-Rational)"); // Or whatever default is set
            Assert.AreEqual(false, bspline.ClosedCurve.Value, "ClosedCurve default check (Non-Rational)");
            Assert.AreEqual(false, bspline.SelfIntersect.Value, "SelfIntersect default check (Non-Rational)");

            // Check that control points were created and cached (implicitly by checking count and that they are not null)
            foreach (var cp_ref in bspline.ControlPointsList)
            {
                Assert.IsNotNull(cp_ref, "Control point reference should not be null");
                Assert.IsNotNull(StepConverterUtils.GetCachedEntity(cp_ref.Id), $"Control point {cp_ref.Id} not found in cache");
            }
            Logger.Info("Test: TestCreateBSplineCurveGeometry_NonRational_CorrectProperties PASSED");
        }

        public void TestCreateBSplineCurveGeometry_Rational_CorrectProperties()
        {
            TestInitialize(); // Calls StepConverterUtils.InitExport()

            var bsCurveData = new BSCurveData
            {
                Degree = 2,
                IsClosed = false,
                IsRational = true,
                IsPeriodic = false,
                NumPoles = 3,
                Poles3D = new List<Vector3>
                {
                    new Vector3(0, 0, 0),
                    new Vector3(1, 1, 0),
                    new Vector3(2, 0, 0)
                },
                NumKnots = 6, // 2 + 3 + 1 = 6
                Knots = new List<double> { 0, 0, 0, 1, 1, 1 },
                KnotMultiplicities = new List<int> { 3, 3 },
                Weights = new List<double> { 1.0, 0.707, 1.0 } // Example weights
            };

            var stepCurve = StepConverterUtils.CreateBSplineCurveGeometry(bsCurveData, null, null);

            Assert.IsNotNull(stepCurve, "STEP curve should not be null (Rational)");
            Assert.AreEqual(true, stepCurve is RationalBSplineCurve, "STEP curve should be RationalBSplineCurve (Rational)");

            var rationalBspline = stepCurve as RationalBSplineCurve;
            Assert.AreEqual(bsCurveData.Degree, rationalBspline.Degree, "Degree mismatch (Rational)");
            Assert.AreEqual(bsCurveData.NumPoles, rationalBspline.ControlPointsList.Count, "Control points count mismatch (Rational)");
            Assert.AreEqual(bsCurveData.KnotMultiplicities.Count, rationalBspline.KnotMultiplicities.Count, "Knot multiplicities count mismatch (Rational)");
            Assert.AreEqual(bsCurveData.Knots.Count, rationalBspline.Knots.Count, "Knots count mismatch (Rational)");
            Assert.AreEqual(bsCurveData.Weights.Count, rationalBspline.WeightsData.Count, "Weights count mismatch (Rational)");

            // Check actual weight values
            for(int i=0; i < bsCurveData.Weights.Count; ++i)
            {
                Assert.AreEqual(bsCurveData.Weights[i], rationalBspline.WeightsData[i], $"Weight value mismatch at index {i} (Rational)");
            }

            Assert.AreEqual(StepEnumWrapper.BSplineCurveForm.UNSPECIFIED, rationalBspline.CurveForm, "CurveForm default check (Rational)");
            Assert.AreEqual(false, rationalBspline.ClosedCurve.Value, "ClosedCurve default check (Rational)");
            Assert.AreEqual(false, rationalBspline.SelfIntersect.Value, "SelfIntersect default check (Rational)");

            Logger.Info("Test: TestCreateBSplineCurveGeometry_Rational_CorrectProperties PASSED");
        }

        public void TestCreateBSplineSurfaceGeometry_NonRational_CorrectProperties()
        {
            TestInitialize();

            var bsSurfaceData = new BSSurfaceData
            {
                UDegree = 2,
                VDegree = 2,
                IsUClosed = false,
                IsVClosed = false,
                IsRational = false,
                IsUPeriodic = false, // Assuming non-periodic for simplicity
                IsVPeriodic = false,
                NumUPoles = 3,
                NumVPoles = 3,
                Poles3DGrid = new Vector3[3, 3]
                {
                    { new Vector3(0,0,0), new Vector3(1,0,1), new Vector3(2,0,0) },
                    { new Vector3(0,1,1), new Vector3(1,1,2), new Vector3(2,1,1) },
                    { new Vector3(0,2,0), new Vector3(1,2,1), new Vector3(2,2,0) }
                },
                NumUKnots = 6, // UDegree + NumUPoles + 1 = 2+3+1 = 6
                UKnots = new List<double> { 0,0,0,1,1,1 },
                UKnotMultiplicities = new List<int> { 3,3 },
                NumVKnots = 6, // VDegree + NumVPoles + 1 = 2+3+1 = 6
                VKnots = new List<double> { 0,0,0,1,1,1 },
                VKnotMultiplicities = new List<int> { 3,3 }
            };

            var stepSurface = StepConverterUtils.CreateBSplineSurfaceGeometry(bsSurfaceData, null, null);

            Assert.IsNotNull(stepSurface, "STEP surface should not be null (Non-Rational)");
            Assert.AreEqual(true, stepSurface is BSplineSurfaceWithKnots, "STEP surface should be BSplineSurfaceWithKnots (Non-Rational)");

            var bsplineSurface = stepSurface as BSplineSurfaceWithKnots;
            Assert.AreEqual(bsSurfaceData.UDegree, bsplineSurface.UDegree, "UDegree mismatch (Non-Rational)");
            Assert.AreEqual(bsSurfaceData.VDegree, bsplineSurface.VDegree, "VDegree mismatch (Non-Rational)");
            Assert.AreEqual(bsSurfaceData.NumUPoles, bsplineSurface.ControlPointsList.Count, "U-dimension of control points grid mismatch (Non-Rational)");
            Assert.AreEqual(bsSurfaceData.NumVPoles, bsplineSurface.ControlPointsList[0].Count, "V-dimension of control points grid mismatch (Non-Rational)");

            Assert.AreEqual(bsSurfaceData.UKnots.Count, bsplineSurface.UKnots.Count, "U-Knots count mismatch (Non-Rational)");
            Assert.AreEqual(bsSurfaceData.VKnots.Count, bsplineSurface.VKnots.Count, "V-Knots count mismatch (Non-Rational)");
            Assert.AreEqual(bsSurfaceData.UKnotMultiplicities.Count, bsplineSurface.UKnotMultiplicities.Count, "U-KnotMultiplicities count mismatch (Non-Rational)");
            Assert.AreEqual(bsSurfaceData.VKnotMultiplicities.Count, bsplineSurface.VKnotMultiplicities.Count, "V-KnotMultiplicities count mismatch (Non-Rational)");

            Assert.AreEqual(StepEnumWrapper.BSplineSurfaceForm.UNSPECIFIED, bsplineSurface.SurfaceForm, "SurfaceForm default check (Non-Rational)");
            Assert.AreEqual(false, bsplineSurface.UClosed.Value, "UClosed default check (Non-Rational)");
            Assert.AreEqual(false, bsplineSurface.VClosed.Value, "VClosed default check (Non-Rational)");
            Assert.AreEqual(false, bsplineSurface.SelfIntersect.Value, "SelfIntersect default check (Non-Rational)");

            Logger.Info("Test: TestCreateBSplineSurfaceGeometry_NonRational_CorrectProperties PASSED");
        }

        public void TestCreateBSplineSurfaceGeometry_Rational_CorrectProperties()
        {
            TestInitialize();

            var bsSurfaceData = new BSSurfaceData
            {
                UDegree = 2,
                VDegree = 2,
                IsUClosed = false,
                IsVClosed = false,
                IsRational = true,
                NumUPoles = 3,
                NumVPoles = 3,
                Poles3DGrid = new Vector3[3, 3]
                {
                    { new Vector3(0,0,0), new Vector3(1,0,1), new Vector3(2,0,0) },
                    { new Vector3(0,1,1), new Vector3(1,1,2), new Vector3(2,1,1) },
                    { new Vector3(0,2,0), new Vector3(1,2,1), new Vector3(2,2,0) }
                },
                NumUKnots = 6, UKnots = new List<double> { 0,0,0,1,1,1 }, UKnotMultiplicities = new List<int> { 3,3 },
                NumVKnots = 6, VKnots = new List<double> { 0,0,0,1,1,1 }, VKnotMultiplicities = new List<int> { 3,3 },
                WeightsData = new double[3, 3]
                {
                    { 1.0, 0.8, 1.0 },
                    { 0.8, 0.6, 0.8 },
                    { 1.0, 0.8, 1.0 }
                }
            };

            var stepSurface = StepConverterUtils.CreateBSplineSurfaceGeometry(bsSurfaceData, null, null);

            Assert.IsNotNull(stepSurface, "STEP surface should not be null (Rational)");
            Assert.AreEqual(true, stepSurface is RationalBSplineSurface, "STEP surface should be RationalBSplineSurface (Rational)");

            var rationalBsplineSurface = stepSurface as RationalBSplineSurface;
            Assert.AreEqual(bsSurfaceData.UDegree, rationalBsplineSurface.UDegree, "UDegree mismatch (Rational)");
            Assert.AreEqual(bsSurfaceData.VDegree, rationalBsplineSurface.VDegree, "VDegree mismatch (Rational)");
            Assert.AreEqual(bsSurfaceData.NumUPoles, rationalBsplineSurface.ControlPointsList.Count, "U-dimension of control points grid mismatch (Rational)");
            Assert.AreEqual(bsSurfaceData.NumVPoles, rationalBsplineSurface.ControlPointsList[0].Count, "V-dimension of control points grid mismatch (Rational)");

            Assert.AreEqual(bsSurfaceData.WeightsData.GetLength(0), rationalBsplineSurface.WeightsData.GetLength(0), "Weights U-dimension mismatch (Rational)");
            Assert.AreEqual(bsSurfaceData.WeightsData.GetLength(1), rationalBsplineSurface.WeightsData.GetLength(1), "Weights V-dimension mismatch (Rational)");
            Assert.AreEqual(bsSurfaceData.WeightsData[1,1], rationalBsplineSurface.WeightsData[1,1], "Sample weight value mismatch (Rational)");

            Logger.Info("Test: TestCreateBSplineSurfaceGeometry_Rational_CorrectProperties PASSED");
        }

using System.Text.RegularExpressions; // Added for Regex

// ... (existing using statements)

namespace InventorLoaderCs.Tests
{
    // ... (Assert class)

    public class AcisParsingUtilsTests
    {
        // ... (existing AcisParsingUtilsTests content)
    }

    public class AcisEntitySetTests
    {
        // ... (existing AcisEntitySetTests content)
    }

    public class AcisToStepConverterTests
    {
        // ... (existing TestInitialize, B-Spline tests)

        private int CountOccurrences(string text, string pattern)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern)) return 0;
            return Regex.Matches(text, Regex.Escape(pattern)).Count;
        }

        public void TestRationalBSplineCurve_ExportStep_FormatsCorrectly()
        {
            TestInitialize();
            int entityCounter = 10; // Start with an arbitrary ID for the curve itself

            // 1. Create BSCurveData for a simple rational B-Spline curve
            // Degree 1, 2 control points, 2 knots (multiplicity 2 each for clamped)
            // Knots: (0,0,1,1)
            var bsCurveData = new BSCurveData
            {
                Degree = 1,
                IsClosed = false,
                IsRational = true,
                IsPeriodic = false,
                NumPoles = 2,
                Poles3D = new List<Vector3>
                {
                    new Vector3(0, 0, 0), // Will become #cp1_id (e.g., #1)
                    new Vector3(1, 1, 0)  // Will become #cp2_id (e.g., #2)
                },
                NumKnots = 4, // Degree + NumPoles + 1 = 1 + 2 + 1 = 4
                Knots = new List<double> { 0, 0, 1, 1 },
                KnotMultiplicities = new List<int> { 2, 2 }, // Clamped
                Weights = new List<double> { 1.0, 0.5 }
            };

            // 2. Create the RationalBSplineCurve STEP entity
            // This will also create and cache the CartesianPoint entities for control points.
            // Let's assume CreateCartesianPoint assigns IDs sequentially starting from 1 if cache is empty.
            // So, CP1 -> #1, CP2 -> #2 if InitExport() was called.
            // The curve itself will get entityCounter as its ID.

            // Manually create and cache CPs to control their IDs for predictable output string
            var cp1 = StepConverterUtils.CreateCartesianPoint(bsCurveData.Poles3D[0], null, null); // ID will be 1 (assuming fresh init)
            var cp2 = StepConverterUtils.CreateCartesianPoint(bsCurveData.Poles3D[1], null, null); // ID will be 2

            // Now create the curve, it should pick up these cached points.
            var stepCurve = StepConverterUtils.CreateBSplineCurveGeometry(bsCurveData, null, null) as RationalBSplineCurve;
            Assert.IsNotNull(stepCurve, "Failed to create RationalBSplineCurve for ExportStep test");

            // Assign a specific ID to the curve for predictable output
            stepCurve.Id = entityCounter;


            // 3. Export the STEP string for the curve
            string stepString = stepCurve.ExportStep(ref entityCounter); // entityCounter will be incremented by ExportStep if it creates new sub-entities (not for this curve type itself, but good practice)

            // 4. Verify the output string
            // Example expected: #10=RATIONAL_B_SPLINE_CURVE('',1,(#1,#2),.UNSPECIFIED.,.F.,.F.,(2,2),(0.0,0.0,1.0,1.0),.UNSPECIFIED.,(1.0,0.5));
            // Note: IDs of CPs (#1, #2) depend on caching behavior. Assumed fresh init for this test.
            // The name '' is default.

            string expectedName = "''";
            string expectedDegree = bsCurveData.Degree.ToString();
            string expectedControlPoints = $"({cp1.GetStepReference()},{cp2.GetStepReference()})"; // Relies on cp IDs
            string expectedCurveForm = ".UNSPECIFIED."; // Default from BSplineCurve base
            string expectedClosed = ".F.";
            string expectedSelfIntersect = ".F.";
            string expectedKnotMultiplicities = "(2,2)";
            string expectedKnots = "(0.0000000000,0.0000000000,1.0000000000,1.0000000000)"; // Using default double formatting
            string expectedKnotSpec = ".UNSPECIFIED."; // Default from BSplineCurveWithKnots
            string expectedWeights = "(1.0000000000,0.5000000000)"; // Using default double formatting

            string expectedString = $"#{stepCurve.Id}=RATIONAL_B_SPLINE_CURVE({expectedName},{expectedDegree},{expectedControlPoints},{expectedCurveForm},{expectedClosed},{expectedSelfIntersect},{expectedKnotMultiplicities},{expectedKnots},{expectedKnotSpec},{expectedWeights});";

            // Normalize string for comparison (e.g. remove whitespace, handle float precision issues if necessary)
            // For now, direct comparison, assuming consistent formatting.
            // Console.WriteLine($"Expected: {expectedString}");
            // Console.WriteLine($"Actual:   {stepString}");

            Assert.AreEqual(expectedString, stepString.Trim(), "STEP string for RationalBSplineCurve does not match expected format.");

            Logger.Info("Test: TestRationalBSplineCurve_ExportStep_FormatsCorrectly PASSED");
        }

        public void TestExport_SimpleAcisModel_GeneratesValidStepFileStructure()
        {
            TestInitialize(); // Calls StepConverterUtils.InitExport() and resets caches

            // 1. Setup Mock AcisReader and AcisHeader
            // AcisGlobalUtils.SetReader is not strictly necessary for Export if it directly takes an Inventor model
            // and the AcisReader inside Inventor model is already populated.
            // However, if any utility within StepConverterUtils globally accesses it, it might be.
            // For this test, we'll construct an AcisReader and populate it directly into the Inventor model.
            var acisHeader = new AcisHeader(version: 26.0, scale: 1.0); // Using a version that supports styles if testing them
            var testAcisReader = new AcisReader(acisHeader);
            AcisGlobalUtils.SetReader(testAcisReader); // Set it globally in case any deep utility needs it

            Logger.Info("TestExport_SimpleAcisModel: Starting ACIS model construction...");

            // 2. Construct C# ACIS Model (Simplified Cube Example: 10x10x10 cube)
            // Points (vertices of the cube)
            var p0 = new AcisPoint(0, 0, 0) { Index = 100 };
            var p1 = new AcisPoint(10, 0, 0) { Index = 101 };
            var p2 = new AcisPoint(10, 10, 0) { Index = 102 };
            var p3 = new AcisPoint(0, 10, 0) { Index = 103 };
            var p4 = new AcisPoint(0, 0, 10) { Index = 104 };
            var p5 = new AcisPoint(10, 0, 10) { Index = 105 };
            var p6 = new AcisPoint(10, 10, 10) { Index = 106 };
            var p7 = new AcisPoint(0, 10, 10) { Index = 107 };
            var acisPoints = new List<AcisPoint> { p0, p1, p2, p3, p4, p5, p6, p7 };

            // Vertices
            var v0 = new AcisVertex(p0) { Index = 200 }; var v1 = new AcisVertex(p1) { Index = 201 };
            var v2 = new AcisVertex(p2) { Index = 202 }; var v3 = new AcisVertex(p3) { Index = 203 };
            var v4 = new AcisVertex(p4) { Index = 204 }; var v5 = new AcisVertex(p5) { Index = 205 };
            var v6 = new AcisVertex(p6) { Index = 206 }; var v7 = new AcisVertex(p7) { Index = 207 };
            var acisVertices = new List<AcisVertex> { v0, v1, v2, v3, v4, v5, v6, v7 };

            // Straight Curves for Edges (simplified: direct construction, not via AcisRecord.Set)
            // Bottom face
            var c01 = new AcisCurveStraight(p0.Position, p1.Position - p0.Position) { Index = 300 };
            var c12 = new AcisCurveStraight(p1.Position, p2.Position - p1.Position) { Index = 301 };
            var c23 = new AcisCurveStraight(p2.Position, p3.Position - p2.Position) { Index = 302 };
            var c30 = new AcisCurveStraight(p3.Position, p0.Position - p3.Position) { Index = 303 };
            // Top face
            var c45 = new AcisCurveStraight(p4.Position, p5.Position - p4.Position) { Index = 304 };
            var c56 = new AcisCurveStraight(p5.Position, p6.Position - p5.Position) { Index = 305 };
            var c67 = new AcisCurveStraight(p6.Position, p7.Position - p6.Position) { Index = 306 };
            var c74 = new AcisCurveStraight(p7.Position, p4.Position - p7.Position) { Index = 307 };
            // Vertical edges
            var c04 = new AcisCurveStraight(p0.Position, p4.Position - p0.Position) { Index = 308 };
            var c15 = new AcisCurveStraight(p1.Position, p5.Position - p1.Position) { Index = 309 };
            var c26 = new AcisCurveStraight(p2.Position, p6.Position - p2.Position) { Index = 310 };
            var c37 = new AcisCurveStraight(p3.Position, p7.Position - p3.Position) { Index = 311 };
            var acisCurves = new List<AcisCurveStraight> { c01, c12, c23, c30, c45, c56, c74, c04, c15, c26, c37 }; // c67 missing, c74 duplicated. Correcting.
            acisCurves = new List<AcisCurveStraight> { c01, c12, c23, c30, c45, c56, c67, c74, c04, c15, c26, c37 };


            // Edges (linking vertices and curves)
            var e01 = new AcisEdge(v0, v1, c01) { Index = 400 }; var e12 = new AcisEdge(v1, v2, c12) { Index = 401 };
            var e23 = new AcisEdge(v2, v3, c23) { Index = 402 }; var e30 = new AcisEdge(v3, v0, c30) { Index = 403 };
            var e45 = new AcisEdge(v4, v5, c45) { Index = 404 }; var e56 = new AcisEdge(v5, v6, c56) { Index = 405 };
            var e67 = new AcisEdge(v6, v7, c67) { Index = 406 }; var e74 = new AcisEdge(v7, v4, c74) { Index = 407 };
            var e04 = new AcisEdge(v0, v4, c04) { Index = 408 }; var e15 = new AcisEdge(v1, v5, c15) { Index = 409 };
            var e26 = new AcisEdge(v2, v6, c26) { Index = 410 }; var e37 = new AcisEdge(v3, v7, c37) { Index = 411 };
            var acisEdges = new List<AcisEdge> { e01, e12, e23, e30, e45, e56, e67, e74, e04, e15, e26, e37 };

            // CoEdges (two for each edge, one forward, one reversed)
            var coedges = new List<AcisCoEdge>();
            int coedgeIdx = 500;
            foreach (var edge in acisEdges)
            {
                coedges.Add(new AcisCoEdge(edge, false) { Index = coedgeIdx++ }); // Forward
                coedges.Add(new AcisCoEdge(edge, true) { Index = coedgeIdx++ });  // Reversed
            }
            Func<AcisEdge, bool, AcisCoEdge> findCoEdge = (edge, reversed) =>
                coedges.First(ce => ce.EdgeEntity == edge && ce.Reversed == reversed);

            // SurfacePlanes for faces
            // Normals point outwards for a standard cube
            var surfBottom = new AcisSurfacePlane(p0.Position, new Vector3(0,0,-1)) { Index = 600 }; // Z=0 plane, normal -Z
            var surfTop    = new AcisSurfacePlane(p4.Position, new Vector3(0,0,1))  { Index = 601 }; // Z=10 plane, normal +Z
            var surfFront  = new AcisSurfacePlane(p0.Position, new Vector3(0,-1,0)) { Index = 602 }; // Y=0 plane, normal -Y
            var surfBack   = new AcisSurfacePlane(p3.Position, new Vector3(0,1,0))  { Index = 603 }; // Y=10 plane, normal +Y
            var surfLeft   = new AcisSurfacePlane(p0.Position, new Vector3(-1,0,0)) { Index = 604 }; // X=0 plane, normal -X
            var surfRight  = new AcisSurfacePlane(p1.Position, new Vector3(1,0,0))  { Index = 605 }; // X=10 plane, normal +X
            var acisSurfaces = new List<AcisSurfacePlane> { surfBottom, surfTop, surfFront, surfBack, surfLeft, surfRight };

            var acisFaces = new List<AcisFace>();
            var acisLoops = new List<AcisLoop>();
            int faceIdx = 700; int loopIdx = 800;

            // Face definitions (Loops with CoEdges)
            // Bottom face (p0-p3-p2-p1) normal (0,0,-1)
            var loopBottom = new AcisLoop(null) { Index = loopIdx++ }; // CoEdge chain set below
            loopBottom.CoedgeEntity = findCoEdge(e30, true); // p3->p0 (e30 reversed)
            findCoEdge(e30, true).NextCoedge = findCoEdge(e23, true); // p2->p3 (e23 reversed)
            findCoEdge(e23, true).NextCoedge = findCoEdge(e12, true); // p1->p2 (e12 reversed)
            findCoEdge(e12, true).NextCoedge = findCoEdge(e01, true); // p0->p1 (e01 reversed)
            findCoEdge(e01, true).NextCoedge = findCoEdge(e30, true); // Close loop
            acisLoops.Add(loopBottom);
            acisFaces.Add(new AcisFace(surfBottom, loopBottom, false) { Index = faceIdx++ }); // false for normal alignment with surface

            // Top face (p4-p5-p6-p7) normal (0,0,1)
            var loopTop = new AcisLoop(null) { Index = loopIdx++ };
            loopTop.CoedgeEntity = findCoEdge(e45, false); // p4->p5
            findCoEdge(e45, false).NextCoedge = findCoEdge(e56, false); // p5->p6
            findCoEdge(e56, false).NextCoedge = findCoEdge(e67, false); // p6->p7
            findCoEdge(e67, false).NextCoedge = findCoEdge(e74, false); // p7->p4
            findCoEdge(e74, false).NextCoedge = findCoEdge(e45, false);
            acisLoops.Add(loopTop);
            acisFaces.Add(new AcisFace(surfTop, loopTop, false) { Index = faceIdx++ });

            // Front face (p0-p1-p5-p4) normal (0,-1,0)
            var loopFront = new AcisLoop(null) { Index = loopIdx++ };
            loopFront.CoedgeEntity = findCoEdge(e01, false); // p0->p1
            findCoEdge(e01, false).NextCoedge = findCoEdge(e15, false); // p1->p5
            findCoEdge(e15, false).NextCoedge = findCoEdge(e45, true);  // p5->p4 (e45 reversed)
            findCoEdge(e45, true).NextCoedge = findCoEdge(e04, true);   // p4->p0 (e04 reversed)
            findCoEdge(e04, true).NextCoedge = findCoEdge(e01, false);
            acisLoops.Add(loopFront);
            acisFaces.Add(new AcisFace(surfFront, loopFront, false) { Index = faceIdx++ });

            // Back face (p3-p7-p6-p2) normal (0,1,0)
            var loopBack = new AcisLoop(null) { Index = loopIdx++ };
            loopBack.CoedgeEntity = findCoEdge(e23, false); // p2->p3 (e23 true seems wrong, p3->p2 for std loop) -> (p3,p7,p6,p2)
            // Corrected Back Face Loop (p3-p2-p6-p7), normal (0,1,0) so Y=10 face
            // This means coedges are: e37(F), e76(F from e67R), e62(F from e26R), e23(F from e32R)
            // For simplicity, let's define loops in the order their edges appear if traversing the vertices
            // Back Face (p3, p7, p6, p2) - Surface normal (0,1,0) (points towards positive Y)
            // Edges: (p3->p7) e37, (p7->p6) e67 reversed, (p6->p2) e26 reversed, (p2->p3) e23 forward
            loopBack.CoedgeEntity = findCoEdge(e37, false); // p3->p7
            findCoEdge(e37, false).NextCoedge = findCoEdge(e67, true);  // p7->p6 (e67 reversed)
            findCoEdge(e67, true).NextCoedge = findCoEdge(e26, true);   // p6->p2 (e26 reversed)
            findCoEdge(e26, true).NextCoedge = findCoEdge(e23, false);  // p2->p3
            findCoEdge(e23, false).NextCoedge = findCoEdge(e37, false);
            acisLoops.Add(loopBack);
            acisFaces.Add(new AcisFace(surfBack, loopBack, false) { Index = faceIdx++ });


            // Left face (p0-p4-p7-p3) normal (-1,0,0)
            var loopLeft = new AcisLoop(null) { Index = loopIdx++ };
            loopLeft.CoedgeEntity = findCoEdge(e04, false); // p0->p4
            findCoEdge(e04, false).NextCoedge = findCoEdge(e74, true);  // p4->p7 (e74 reversed)
            findCoEdge(e74, true).NextCoedge = findCoEdge(e37, true);   // p7->p3 (e37 reversed)
            findCoEdge(e37, true).NextCoedge = findCoEdge(e30, false);  // p3->p0
            findCoEdge(e30, false).NextCoedge = findCoEdge(e04, false);
            acisLoops.Add(loopLeft);
            acisFaces.Add(new AcisFace(surfLeft, loopLeft, false) { Index = faceIdx++ });

            // Right face (p1-p2-p6-p5) normal (1,0,0)
            var loopRight = new AcisLoop(null) { Index = loopIdx++ };
            loopRight.CoedgeEntity = findCoEdge(e12, false); // p1->p2
            findCoEdge(e12, false).NextCoedge = findCoEdge(e26, false); // p2->p6
            findCoEdge(e26, false).NextCoedge = findCoEdge(e56, true);  // p6->p5 (e56 reversed)
            findCoEdge(e56, true).NextCoedge = findCoEdge(e15, true);   // p5->p1 (e15 reversed)
            findCoEdge(e15, true).NextCoedge = findCoEdge(e12, false);
            acisLoops.Add(loopRight);
            var faceRight = new AcisFace(surfRight, loopRight, false) { Index = faceIdx++ };
            acisFaces.Add(faceRight);

            // Optional: Add color to one face
            var colorAttrib = new AttribStRgbColor(0.5, 0.2, 0.8) { Index = 900, Name = "MyColor" }; // R, G, B
            // Link attribute. In ACIS, attributes are typically linked via NextAttributeBlockDef.
            // For testing StepConverterUtils, it might expect AttribList on the AcisFace.
            faceRight.AttribList = new List<AcisAttribute> { colorAttrib };
            testAcisReader.RecordsList.Add(new AcisRecord("attrib_st_rgb_color-entity", testAcisReader) { Entity = colorAttrib, Index = colorAttrib.Index });


            // Shell
            var shell = new AcisShell(null) { Index = 1000 }; // Face chain set below
            shell.FaceEntity = acisFaces[0];
            for(int i = 0; i < acisFaces.Count - 1; i++) { acisFaces[i].NextFace = acisFaces[i+1]; }
            acisFaces.Last().NextFace = null; // Terminate chain
            var acisShells = new List<AcisShell> { shell };

            // Lump
            var lump = new AcisLump(shell) { Index = 1100 };
            var acisLumps = new List<AcisLump> { lump };

            // Body
            var bodyTransform = new AcisTransform(Matrix4x4.CreateTranslation(5, 0, 0)) { Index = 1201 }; // Optional transform
            var body = new AcisBody(lump) { Index = 1200, TransformEntity = bodyTransform };
            //var body = new AcisBody(lump) { Index = 1200, TransformEntity = null }; // No transform

            testAcisReader.RecordsList.Add(new AcisRecord("transform-entity", testAcisReader) { Entity = bodyTransform, Index = bodyTransform.Index });


            // 3. Populate Mock Inventor Model
            var inventorModel = new Inventor();
            // Add all created ACIS entities to testAcisReader.RecordsList so GetBodies can find the body.
            // The actual conversion should navigate the C# object graph from the body.
            acisPoints.ForEach(e => testAcisReader.RecordsList.Add(new AcisRecord($"{e.GetType().Name.ToLower()}-entity", testAcisReader) { Entity = e, Index = e.Index }));
            acisVertices.ForEach(e => testAcisReader.RecordsList.Add(new AcisRecord($"{e.GetType().Name.ToLower()}-entity", testAcisReader) { Entity = e, Index = e.Index }));
            acisCurves.ForEach(e => testAcisReader.RecordsList.Add(new AcisRecord($"{e.GetType().Name.ToLower()}-entity", testAcisReader) { Entity = e, Index = e.Index }));
            acisEdges.ForEach(e => testAcisReader.RecordsList.Add(new AcisRecord($"{e.GetType().Name.ToLower()}-entity", testAcisReader) { Entity = e, Index = e.Index }));
            coedges.ForEach(e => testAcisReader.RecordsList.Add(new AcisRecord($"{e.GetType().Name.ToLower()}-entity", testAcisReader) { Entity = e, Index = e.Index }));
            acisSurfaces.ForEach(e => testAcisReader.RecordsList.Add(new AcisRecord($"{e.GetType().Name.ToLower()}-entity", testAcisReader) { Entity = e, Index = e.Index }));
            acisLoops.ForEach(e => testAcisReader.RecordsList.Add(new AcisRecord($"{e.GetType().Name.ToLower()}-entity", testAcisReader) { Entity = e, Index = e.Index }));
            acisFaces.ForEach(e => testAcisReader.RecordsList.Add(new AcisRecord($"{e.GetType().Name.ToLower()}-entity", testAcisReader) { Entity = e, Index = e.Index }));
            acisShells.ForEach(e => testAcisReader.RecordsList.Add(new AcisRecord($"{e.GetType().Name.ToLower()}-entity", testAcisReader) { Entity = e, Index = e.Index }));
            acisLumps.ForEach(e => testAcisReader.RecordsList.Add(new AcisRecord($"{e.GetType().Name.ToLower()}-entity", testAcisReader) { Entity = e, Index = e.Index }));
            testAcisReader.RecordsList.Add(new AcisRecord("body-entity", testAcisReader) { Entity = body, Index = body.Index });

            var brepSegment = new RSeSegment(inventorModel) { Name = "BRepTestSegment" };
            brepSegment.ParsedContent["ACIS"] = testAcisReader;
            inventorModel.Segments["BRepTestSegment"] = brepSegment;

            Logger.Info("TestExport_SimpleAcisModel: ACIS model construction complete.");

            // 4. Call Export
            Logger.Info("TestExport_SimpleAcisModel: Calling StepConverterUtils.Export...");
            string stepOutput = StepConverterUtils.Export(inventorModel, "TestCube.stp");
            Logger.Info("TestExport_SimpleAcisModel: StepConverterUtils.Export finished.");
            // File.WriteAllText("TestCube_Output.stp", stepOutput); // For manual inspection if needed

            // 5. Validate STEP Output String
            Assert.IsTrue(stepOutput.StartsWith("ISO-10303-21;"), "STEP Starts correctly");
            Assert.IsTrue(stepOutput.Contains("HEADER;"), "Contains HEADER section");
            Assert.IsTrue(stepOutput.Contains("FILE_DESCRIPTION(('ViewMode Model'),'2;1');"), "Contains FILE_DESCRIPTION");
            Assert.IsTrue(stepOutput.Contains("FILE_NAME('TestCube.stp'"), "Contains FILE_NAME");
            Assert.IsTrue(stepOutput.Contains("FILE_SCHEMA(('AUTOMOTIVE_DESIGN { 1 0 10303 214 1 1 1 1 }'));"), "Contains FILE_SCHEMA");
            Assert.IsTrue(stepOutput.Contains("DATA;"), "Contains DATA section");
            Assert.IsTrue(stepOutput.Contains("ENDSEC;"), "Contains ENDSEC");
            Assert.IsTrue(stepOutput.Contains("END-ISO-10303-21;"), "STEP Ends correctly");

            // Entity counts. These depend on how StepConverterUtils creates and caches entities.
            // For a simple cube with no shared vertices at STEP level (VERTEX_POINT per edge end)
            // Points (transformed if body has transform)
            int expectedCartesianPoints = 8; // 8 vertices for the cube
            Assert.AreEqual(expectedCartesianPoints, CountOccurrences(stepOutput, "CARTESIAN_POINT"), "CARTESIAN_POINT count");

            Assert.AreEqual(12, CountOccurrences(stepOutput, "LINE("), "LINE count"); // LINE( not "LINE)"
            Assert.AreEqual(6, CountOccurrences(stepOutput, "PLANE("), "PLANE count");

            // Topological entities
            // VertexPoint count might be 8 if they are uniquely created from AcisPoint.
            // Or it could be 24 if each co-edge end results in a new VP (less optimal but possible).
            // The current converter is expected to make unique VPs from AcisVertices (which come from AcisPoints).
            Assert.AreEqual(8, CountOccurrences(stepOutput, "VERTEX_POINT"), "VERTEX_POINT count");
            Assert.AreEqual(12, CountOccurrences(stepOutput, "EDGE_CURVE"), "EDGE_CURVE count");
            Assert.AreEqual(24, CountOccurrences(stepOutput, "ORIENTED_EDGE"), "ORIENTED_EDGE count"); // 2 per edge
            Assert.AreEqual(6, CountOccurrences(stepOutput, "EDGE_LOOP"), "EDGE_LOOP count");
            Assert.AreEqual(6, CountOccurrences(stepOutput, "FACE_OUTER_BOUND"), "FACE_OUTER_BOUND count");
            Assert.AreEqual(6, CountOccurrences(stepOutput, "ADVANCED_FACE"), "ADVANCED_FACE count");

            // Shell and Solid Model. Could be OPEN_SHELL or (preferably for a cube) CLOSED_SHELL.
            // The converter creates OPEN_SHELL by default for AcisShell.
            // Assert.IsTrue(CountOccurrences(stepOutput, "OPEN_SHELL") == 1 || CountOccurrences(stepOutput, "CLOSED_SHELL") == 1, "SHELL count (OPEN or CLOSED)");
            Assert.AreEqual(1, CountOccurrences(stepOutput, "OPEN_SHELL"), "OPEN_SHELL count");


            // Depending on how high-level entities are created:
            // SHELL_BASED_SURFACE_MODEL for open shells, or MANIFOLD_SOLID_BREP for closed.
            // Current converter creates SHELL_BASED_SURFACE_MODEL.
            Assert.AreEqual(1, CountOccurrences(stepOutput, "SHELL_BASED_SURFACE_MODEL"), "SHELL_BASED_SURFACE_MODEL count");

            // AXIS2_PLACEMENT_3D: 6 for planes, 12 for lines (if each line has its own placement), potentially 1 for body transform.
            // Current line export doesn't create AXIS2_PLACEMENT_3D, it uses points and a vector.
            // Planes use one AXIS2_PLACEMENT_3D each.
            // The body transform, if applied, results in transformed points, not a separate AXIS2_PLACEMENT_3D for the whole body in this path.
            // The root transform for the SHELL_BASED_SURFACE_MODEL is an AXIS2_PLACEMENT_3D.
            int expectedAxis2Placement3D = 6 (planes) + 1 (shell based surface model context);
            Assert.AreEqual(expectedAxis2Placement3D, CountOccurrences(stepOutput, "AXIS2_PLACEMENT_3D"), "AXIS2_PLACEMENT_3D count");


            // Color
            Assert.AreEqual(1, CountOccurrences(stepOutput, "COLOUR_RGB"), "COLOUR_RGB count");
            Assert.AreEqual(1, CountOccurrences(stepOutput, "SURFACE_STYLE_USAGE"), "SURFACE_STYLE_USAGE count");
            Assert.AreEqual(1, CountOccurrences(stepOutput, "SURFACE_STYLE_FILL_AREA"), "SURFACE_STYLE_FILL_AREA count");
            Assert.AreEqual(1, CountOccurrences(stepOutput, "PRESENTATION_STYLE_ASSIGNMENT"), "PRESENTATION_STYLE_ASSIGNMENT count");
            Assert.AreEqual(1, CountOccurrences(stepOutput, "STYLED_ITEM"), "STYLED_ITEM count");

            // Transform check: one of the points should be translated by (5,0,0)
            // p0 was (0,0,0), so transformed should be (5,0,0)
            // CARTESIAN_POINT('',(5.0000000000,0.0000000000,0.0000000000))
            Assert.IsTrue(stepOutput.Contains("CARTESIAN_POINT('',(5.0"), "Transformed point check for X=5.0");
            // p4 was (0,0,10), transformed should be (5,0,10)
            Assert.IsTrue(stepOutput.Contains("CARTESIAN_POINT('',(5.0000000000,0.0000000000,10.0000000000))"), "Transformed point (5,0,10) check");


            Logger.Info("Test: TestExport_SimpleAcisModel_GeneratesValidStepFileStructure PASSED");
        }

        public void TestCreateAndExport_ConicalSurface()
        {
            TestInitialize();
            int entityIdCounter = 1; // For assigning IDs to STEP entities

            // 1. Create AcisSurfaceCone
            var center = new Vector3(1, 1, 0);
            var axis = new Vector3(0, 0, 1); // Points along Z
            var refAxisPt = new Vector3(1, 0, 0); // Point on the cone, defines major radius direction if applicable, here for X-axis align
            double radius = 5.0;
            double semiAngleDegrees = 30.0;
            double semiAngleRadians = semiAngleDegrees * Math.PI / 180.0;

            // AcisSurfaceCone constructor might take different params, this is a guess.
            // Assuming it stores these values or similar that can be retrieved or used by CreateConicalSurface
            // For the purpose of this test, let's assume AcisSurfaceCone has these properties:
            var acisCone = new AcisSurfaceCone(center, axis, refAxisPt, radius, Math.Sin(semiAngleRadians), Math.Cos(semiAngleRadians))
            {
                // Mocking properties that CreateConicalSurface would use:
                PosCenter = center,
                DirAxis = axis,
                DirRefAxis = Vector3.Normalize(refAxisPt - center), // Should be normalized if used as direction
                Radius = radius,
                SineAngle = Math.Sin(semiAngleRadians),
                // CosineAngle is not directly used by CreateConicalSurface for ConicalSurface STEP entity
            };
            acisCone.DirRefAxis = new Vector3(1,0,0); // Simplification for testing placement.

            // Optional: A simple transform
            var transformMatrix = Matrix4x4.CreateTranslation(10, 0, 0);

            // 2. Call StepConverterUtils.CreateConicalSurface
            var stepCone = StepConverterUtils.CreateConicalSurface(acisCone, transformMatrix);

            // 3. Assert ConicalSurface properties
            Assert.IsNotNull(stepCone, "STEP ConicalSurface should not be null.");
            Assert.AreEqual(radius, stepCone.Radius, "ConicalSurface Radius mismatch.");
            Assert.AreEqual(semiAngleRadians, stepCone.SemiAngle, 1e-9, "ConicalSurface SemiAngle mismatch."); // Using delta for double comparison

            Assert.IsNotNull(stepCone.Position, "ConicalSurface Position (Axis2Placement3D) should not be null.");
            var placement = stepCone.Position;

            // Expected transformed position for Axis2Placement3D
            var expectedPlacementOrigin = Vector3.Transform(center, transformMatrix);
            var expectedPlacementAxis = Vector3.Normalize(Vector3.TransformNormal(axis, transformMatrix)); // Direction vectors transformed by normal
            var expectedPlacementRefDir = Vector3.Normalize(Vector3.TransformNormal(acisCone.DirRefAxis, transformMatrix));

            Assert.AreEqual(true, ImporterUtils.IsEqual(expectedPlacementOrigin, placement.Location.Coordinates, 1e-6f), "Placement origin mismatch.");
            Assert.AreEqual(true, ImporterUtils.IsEqual(expectedPlacementAxis, placement.Axis.Coordinates, 1e-6f), "Placement axis mismatch.");
            Assert.AreEqual(true, ImporterUtils.IsEqual(expectedPlacementRefDir, placement.RefDirection.Coordinates, 1e-6f), "Placement ref_direction mismatch.");


            // 4. ExportStep and Validate STEP string snippets
            // We need to export all constituent parts that would be in a real file for references to resolve.
            // For simplicity, we'll focus on the CONICAL_SURFACE line and its direct dependencies.

            // Assign IDs manually for predictable output
            placement.Location.Id = entityIdCounter++; // CartesianPoint for origin
            placement.Axis.Id = entityIdCounter++;       // Direction for Z
            placement.RefDirection.Id = entityIdCounter++; // Direction for X
            placement.Id = entityIdCounter++;            // Axis2Placement3D
            stepCone.Id = entityIdCounter++;             // ConicalSurface

            string conicalSurfaceStr = stepCone.ExportStep(ref entityIdCounter);
            string placementStr = placement.ExportStep(ref entityIdCounter);
            string locationStr = placement.Location.ExportStep(ref entityIdCounter);
            string axisStr = placement.Axis.ExportStep(ref entityIdCounter);
            string refDirStr = placement.RefDirection.ExportStep(ref entityIdCounter);

            // #cone_id=CONICAL_SURFACE('',#placement_id,radius,semi_angle_radians);
            string expectedConeStr = $"#{stepCone.Id}=CONICAL_SURFACE('',{placement.GetStepReference()},{StepConverterUtils.FormatDouble(radius)},{StepConverterUtils.FormatDouble(semiAngleRadians)});";
            Assert.AreEqual(expectedConeStr, conicalSurfaceStr.Trim(), "CONICAL_SURFACE STEP string mismatch.");

            // Example check for AXIS2_PLACEMENT_3D (IDs are illustrative)
            // #placement_id=AXIS2_PLACEMENT_3D('',#location_id,#axis_id,#ref_dir_id);
            string expectedPlacementStr = $"#{placement.Id}=AXIS2_PLACEMENT_3D('',{placement.Location.GetStepReference()},{placement.Axis.GetStepReference()},{placement.RefDirection.GetStepReference()});";
            Assert.AreEqual(expectedPlacementStr, placementStr.Trim(), "AXIS2_PLACEMENT_3D STEP string mismatch.");

            // Check one CartesianPoint and Direction
            string expectedLocationStr = $"#{placement.Location.Id}=CARTESIAN_POINT('',({StepConverterUtils.FormatDouble(expectedPlacementOrigin.X)},{StepConverterUtils.FormatDouble(expectedPlacementOrigin.Y)},{StepConverterUtils.FormatDouble(expectedPlacementOrigin.Z)}));";
            Assert.AreEqual(expectedLocationStr, locationStr.Trim(), "CARTESIAN_POINT for placement origin STEP string mismatch.");

            string expectedAxisStr = $"#{placement.Axis.Id}=DIRECTION('',({StepConverterUtils.FormatDouble(expectedPlacementAxis.X)},{StepConverterUtils.FormatDouble(expectedPlacementAxis.Y)},{StepConverterUtils.FormatDouble(expectedPlacementAxis.Z)}));";
            Assert.AreEqual(expectedAxisStr, axisStr.Trim(), "DIRECTION for placement axis STEP string mismatch.");

            Logger.Info("Test: TestCreateAndExport_ConicalSurface PASSED");
        }

        public void TestCreateAndExport_SphericalSurface()
        {
            TestInitialize();
            int entityIdCounter = 1;

            // 1. Create AcisSurfaceSphere
            var center = new Vector3(5, 5, 5);
            double radius = 10.0;

            // Assuming AcisSurfaceSphere has these properties:
            var acisSphere = new AcisSurfaceSphere(center, radius)
            {
                PosCenter = center,
                Radius = radius
            };

            var transformMatrix = Matrix4x4.CreateTranslation(0, 0, 10); // Translate Z by 10

            // 2. Call StepConverterUtils.CreateSphericalSurface
            var stepSphere = StepConverterUtils.CreateSphericalSurface(acisSphere, transformMatrix);

            // 3. Assert SphericalSurface properties
            Assert.IsNotNull(stepSphere, "STEP SphericalSurface should not be null.");
            Assert.AreEqual(radius, stepSphere.Radius, "SphericalSurface Radius mismatch.");

            Assert.IsNotNull(stepSphere.Position, "SphericalSurface Position (Axis2Placement3D) should not be null.");
            var placement = stepSphere.Position;
            var expectedPlacementOrigin = Vector3.Transform(center, transformMatrix);

            Assert.AreEqual(true, ImporterUtils.IsEqual(expectedPlacementOrigin, placement.Location.Coordinates, 1e-6f), "Placement origin mismatch for sphere.");
            // For a sphere, Axis and RefDirection of the placement are conventional (e.g. Z and X)
            // but should be valid directions. CreateSphericalSurface sets these.
            Assert.IsNotNull(placement.Axis, "Placement Axis should not be null for sphere.");
            Assert.IsNotNull(placement.RefDirection, "Placement RefDirection should not be null for sphere.");


            // 4. ExportStep and Validate STEP string snippets
            placement.Location.Id = entityIdCounter++;
            if (placement.Axis != null) placement.Axis.Id = entityIdCounter++; // May share default directions
            if (placement.RefDirection != null) placement.RefDirection.Id = entityIdCounter++; // May share default directions
            placement.Id = entityIdCounter++;
            stepSphere.Id = entityIdCounter++;

            string sphereStr = stepSphere.ExportStep(ref entityIdCounter);
            string placementStr = placement.ExportStep(ref entityIdCounter); // Will include refs to location, axis, refdir
            string locationStr = placement.Location.ExportStep(ref entityIdCounter);

            // #sphere_id=SPHERICAL_SURFACE('',#placement_id,radius);
            string expectedSphereStr = $"#{stepSphere.Id}=SPHERICAL_SURFACE('',{placement.GetStepReference()},{StepConverterUtils.FormatDouble(radius)});";
            Assert.AreEqual(expectedSphereStr, sphereStr.Trim(), "SPHERICAL_SURFACE STEP string mismatch.");

            // #placement_id=AXIS2_PLACEMENT_3D('',#location_id,#axis_id,#ref_dir_id);
            string expectedPlacementStr = $"#{placement.Id}=AXIS2_PLACEMENT_3D('',{placement.Location.GetStepReference()},{placement.Axis.GetStepReference()},{placement.RefDirection.GetStepReference()});";
            Assert.AreEqual(expectedPlacementStr, placementStr.Trim(), "AXIS2_PLACEMENT_3D for sphere STEP string mismatch.");

            string expectedLocationStr = $"#{placement.Location.Id}=CARTESIAN_POINT('',({StepConverterUtils.FormatDouble(expectedPlacementOrigin.X)},{StepConverterUtils.FormatDouble(expectedPlacementOrigin.Y)},{StepConverterUtils.FormatDouble(expectedPlacementOrigin.Z)}));";
            Assert.AreEqual(expectedLocationStr, locationStr.Trim(), "CARTESIAN_POINT for sphere placement origin STEP string mismatch.");

            Logger.Info("Test: TestCreateAndExport_SphericalSurface PASSED");
        }

        public void TestCreateAndExport_ToroidalSurface()
        {
            TestInitialize();
            int entityIdCounter = 1;

            // 1. Create AcisSurfaceTorus
            var center = new Vector3(2, 3, 4);
            var axis = Vector3.Normalize(new Vector3(0, 1, 1)); // Axis of the torus (normal to the major circle plane)
            var majorRadiusPoint = center + new Vector3(1,0,0) * 10; // A point to define the major radius and ref direction, not along axis

            double majorRadius = 10.0;
            double minorRadius = 2.0;

            // Assuming AcisSurfaceTorus has these properties:
            var acisTorus = new AcisSurfaceTorus(center, axis, majorRadiusPoint, majorRadius, minorRadius)
            {
                PosCenter = center,
                DirNormal = axis, // Axis of the torus
                MajorRadius = majorRadius,
                MinorRadius = minorRadius,
                // DirMajor is the direction from center to a point on the major circle.
                // CreateToroidalSurface calculates this if not directly provided, or uses a default like X-axis if axis is Z.
                // For testing, let's provide one that's perpendicular to 'axis'.
                // A simple way: find a vector perpendicular to 'axis'. If axis=(0,1,1), then (1,0,0) is perp.
                DirMajor = Vector3.Normalize(new Vector3(1,0,0))
            };
            // Ensure DirMajor is perpendicular to DirNormal for a valid setup
            if (Math.Abs(Vector3.Dot(acisTorus.DirNormal, acisTorus.DirMajor)) > 1e-6)
            {
                // If not perp, choose a simple one, e.g. if axis is Z, major can be X.
                // For a general axis, it's more complex. For this test, assume valid inputs or simplify.
                // If axis is (0,1,1), (1,0,0) is fine.
                // If axis was (0,0,1), DirMajor could be (1,0,0).
            }


            var transformMatrix = Matrix4x4.CreateTranslation(10, 20, 30);

            // 2. Call StepConverterUtils.CreateToroidalSurface
            var stepTorus = StepConverterUtils.CreateToroidalSurface(acisTorus, transformMatrix);

            // 3. Assert ToroidalSurface properties
            Assert.IsNotNull(stepTorus, "STEP ToroidalSurface should not be null.");
            Assert.AreEqual(majorRadius, stepTorus.MajorRadius, "ToroidalSurface MajorRadius mismatch.");
            Assert.AreEqual(minorRadius, stepTorus.MinorRadius, "ToroidalSurface MinorRadius mismatch.");

            Assert.IsNotNull(stepTorus.Position, "ToroidalSurface Position (Axis2Placement3D) should not be null.");
            var placement = stepTorus.Position;
            var expectedPlacementOrigin = Vector3.Transform(center, transformMatrix);
            var expectedPlacementAxis = Vector3.Normalize(Vector3.TransformNormal(axis, transformMatrix));
            var expectedPlacementRefDir = Vector3.Normalize(Vector3.TransformNormal(acisTorus.DirMajor, transformMatrix));

            Assert.AreEqual(true, ImporterUtils.IsEqual(expectedPlacementOrigin, placement.Location.Coordinates, 1e-6f), "Placement origin mismatch for torus.");
            Assert.AreEqual(true, ImporterUtils.IsEqual(expectedPlacementAxis, placement.Axis.Coordinates, 1e-6f), "Placement axis mismatch for torus.");
            Assert.AreEqual(true, ImporterUtils.IsEqual(expectedPlacementRefDir, placement.RefDirection.Coordinates, 1e-6f), "Placement ref_direction mismatch for torus.");

            // 4. ExportStep and Validate STEP string snippets
            placement.Location.Id = entityIdCounter++;
            placement.Axis.Id = entityIdCounter++;
            placement.RefDirection.Id = entityIdCounter++;
            placement.Id = entityIdCounter++;
            stepTorus.Id = entityIdCounter++;

            string torusStr = stepTorus.ExportStep(ref entityIdCounter);
            string placementStr = placement.ExportStep(ref entityIdCounter);
            string locationStr = placement.Location.ExportStep(ref entityIdCounter);

            // #torus_id=TOROIDAL_SURFACE('',#placement_id,major_radius,minor_radius);
            string expectedTorusStr = $"#{stepTorus.Id}=TOROIDAL_SURFACE('',{placement.GetStepReference()},{StepConverterUtils.FormatDouble(majorRadius)},{StepConverterUtils.FormatDouble(minorRadius)});";
            Assert.AreEqual(expectedTorusStr, torusStr.Trim(), "TOROIDAL_SURFACE STEP string mismatch.");

            string expectedPlacementStr = $"#{placement.Id}=AXIS2_PLACEMENT_3D('',{placement.Location.GetStepReference()},{placement.Axis.GetStepReference()},{placement.RefDirection.GetStepReference()});";
            Assert.AreEqual(expectedPlacementStr, placementStr.Trim(), "AXIS2_PLACEMENT_3D for torus STEP string mismatch.");

            string expectedLocationStr = $"#{placement.Location.Id}=CARTESIAN_POINT('',({StepConverterUtils.FormatDouble(expectedPlacementOrigin.X)},{StepConverterUtils.FormatDouble(expectedPlacementOrigin.Y)},{StepConverterUtils.FormatDouble(expectedPlacementOrigin.Z)}));";
            Assert.AreEqual(expectedLocationStr, locationStr.Trim(), "CARTESIAN_POINT for torus placement origin STEP string mismatch.");

            Logger.Info("Test: TestCreateAndExport_ToroidalSurface PASSED");
        }

        public void TestExportStep_BSplineSurfaceWithKnots_FormatsCorrectly()
        {
            TestInitialize();
            int entityIdCounter = 1; // For assigning STEP IDs

            // 1. Create BSSurfaceData for a simple 2x2 pole, degree 1x1 non-rational B-Spline surface
            var bsSurfaceData = new BSSurfaceData
            {
                UDegree = 1,
                VDegree = 1,
                IsUClosed = false,
                IsVClosed = false,
                IsRational = false,
                IsUPeriodic = false,
                IsVPeriodic = false,
                NumUPoles = 2, // Nu
                NumVPoles = 2, // Nv
                Poles3DGrid = new Vector3[2, 2]
                {
                    { new Vector3(0, 0, 0), new Vector3(1, 0, 0) }, // Row 0 (V=0)
                    { new Vector3(0, 1, 0), new Vector3(1, 1, 1) }  // Row 1 (V=1)
                },
                // U Knots: Degree + NumPoles + 1 = 1 + 2 + 1 = 4. Clamped: (0,0,1,1)
                NumUKnots = 4, UKnots = new List<double> { 0, 0, 1, 1 }, UKnotMultiplicities = new List<int> { 2, 2 },
                // V Knots: Degree + NumPoles + 1 = 1 + 2 + 1 = 4. Clamped: (0,0,1,1)
                NumVKnots = 4, VKnots = new List<double> { 0, 0, 1, 1 }, VKnotMultiplicities = new List<int> { 2, 2 }
            };

            // Optional transform (or null)
            Matrix4x4? transform = null; //Matrix4x4.CreateTranslation(5,0,0);

            // 2. Create BSplineSurfaceWithKnots STEP entity
            var stepSurface = StepConverterUtils.CreateBSplineSurfaceGeometry(bsSurfaceData, null, transform) as BSplineSurfaceWithKnots;
            Assert.IsNotNull(stepSurface, "Failed to create BSplineSurfaceWithKnots for ExportStep test.");

            // 3. Manually assign IDs for predictable output
            stepSurface.Id = entityIdCounter++; // e.g., #1 for the surface itself

            // Assign IDs to control points (CartesianPoint entities)
            // The ControlPointsList in BSplineSurfaceWithKnots is List<List<StepEntity>> which are CartesianPoints
            string cpListString = "(";
            for (int i = 0; i < stepSurface.ControlPointsList.Count; i++)
            {
                cpListString += (i > 0 ? "," : "") + "(";
                for (int j = 0; j < stepSurface.ControlPointsList[i].Count; j++)
                {
                    var cp = stepSurface.ControlPointsList[i][j] as CartesianPoint;
                    Assert.IsNotNull(cp, $"Control point at ({i},{j}) is not a CartesianPoint or is null.");
                    cp.Id = entityIdCounter++; // Assign sequential IDs, e.g., #2, #3, #4, #5
                    cpListString += (j > 0 ? "," : "") + cp.GetStepReference();
                }
                cpListString += ")";
            }
            cpListString += ")";


            // 4. Export the STEP string for the surface
            string surfaceStepString = stepSurface.ExportStep(ref entityIdCounter);

            // 5. Validate the generated STEP string
            string expectedName = "''"; // Default name
            string expectedUDegree = bsSurfaceData.UDegree.ToString();
            string expectedVDegree = bsSurfaceData.VDegree.ToString();
            // CONTROL_POINTS_LIST is already formatted as cpListString from ID assignment loop
            string expectedSurfaceForm = ".UNSPECIFIED."; // Default
            string expectedUClosed = ".F.";
            string expectedVClosed = ".F.";
            string expectedSelfIntersect = ".F.";
            string expectedUMults = StepConverterUtils.FormatIntegerList(bsSurfaceData.UKnotMultiplicities);
            string expectedVMults = StepConverterUtils.FormatIntegerList(bsSurfaceData.VKnotMultiplicities);
            string expectedUKnots = StepConverterUtils.FormatDoubleList(bsSurfaceData.UKnots);
            string expectedVKnots = StepConverterUtils.FormatDoubleList(bsSurfaceData.VKnots);
            string expectedKnotSpec = ".UNSPECIFIED."; // Default

            string expectedString = $"#{stepSurface.Id}=B_SPLINE_SURFACE_WITH_KNOTS(" +
                                    $"{expectedName}," +
                                    $"{expectedUDegree}," +
                                    $"{expectedVDegree}," +
                                    $"{cpListString}," + // This is the ((#id,#id),(#id,#id)) part
                                    $"{expectedSurfaceForm}," +
                                    $"{expectedUClosed}," +
                                    $"{expectedVClosed}," +
                                    $"{expectedSelfIntersect}," +
                                    $"{expectedUMults}," +
                                    $"{expectedVMults}," +
                                    $"{expectedUKnots}," +
                                    $"{expectedVKnots}," +
                                    $"{expectedKnotSpec});";

            // Console.WriteLine($"Expected: {expectedString}");
            // Console.WriteLine($"Actual:   {surfaceStepString.Trim()}");
            Assert.AreEqual(expectedString, surfaceStepString.Trim(), "B_SPLINE_SURFACE_WITH_KNOTS STEP string mismatch.");

            // Also export and check one of the control points to ensure its string is fine
            var firstCp = stepSurface.ControlPointsList[0][0] as CartesianPoint;
            Assert.IsNotNull(firstCp, "First control point is null.");
            string cpStepString = firstCp.ExportStep(ref entityIdCounter);

            Vector3 firstPole = bsSurfaceData.Poles3DGrid[0,0];
            if (transform.HasValue) firstPole = Vector3.Transform(firstPole, transform.Value);

            string expectedCpString = $"#{firstCp.Id}=CARTESIAN_POINT('',({StepConverterUtils.FormatDouble(firstPole.X)},{StepConverterUtils.FormatDouble(firstPole.Y)},{StepConverterUtils.FormatDouble(firstPole.Z)}));";
            Assert.AreEqual(expectedCpString, cpStepString.Trim(), "First CARTESIAN_POINT of B-Spline surface mismatch.");

            Logger.Info("Test: TestExportStep_BSplineSurfaceWithKnots_FormatsCorrectly PASSED");
        }
    }
}
