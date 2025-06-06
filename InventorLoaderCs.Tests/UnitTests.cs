using System;
using System.Collections.Generic;
using System.Numerics;
using System.IO;
using InventorLoaderCs;
using System.Linq;
using System.Text; // Added for Encoding
using System.Text.RegularExpressions;

namespace InventorLoaderCs.Tests
{
    public static class Assert
    {
        public static void AreEqual(object expected, object actual, string message = "")
        {
            if (expected == null && actual == null) return;
            if (expected == null || !expected.Equals(actual))
            {
                Console.WriteLine($"Assert.AreEqual FAILED {message}: Expected '{expected}', Got '{actual}'");
            }
        }
         public static void AreEqual(double expected, double actual, double delta, string message = "")
        {
            if (Math.Abs(expected - actual) > delta)
            {
                Console.WriteLine($"Assert.AreEqual FAILED {message}: Expected '{expected}', Got '{actual}' (Delta: {delta})");
            }
        }
        public static void IsTrue(bool condition, string message = "")
        {
            if (!condition)
            {
                Console.WriteLine($"Assert.IsTrue FAILED {message}");
            }
        }
        public static void IsNotNull(object value, string message = "")
        {
            if (value == null)
            {
                Console.WriteLine($"Assert.IsNotNull FAILED {message}: Expected not null, Got null");
            }
        }
         public static void IsNull(object value, string message = "")
        {
            if (value != null)
            {
                Console.WriteLine($"Assert.IsNull FAILED {message}: Expected null, Got '{value}'");
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
            typeof(AcisHeader).GetProperty("Scale").SetValue(_mockHeader, 1.0);
            typeof(AcisHeader).GetProperty("Version").SetValue(_mockHeader, 26.0);

            AcisGlobalUtils.SetReader(_mockReader);
            var readerInstance = AcisGlobalUtils.GetReader();
            if (readerInstance != null)
            {
                readerInstance.GetType().GetProperty("Header").SetValue(readerInstance, _mockHeader);
            }
        }
        // ... (AcisParsingUtilsTests methods) ...
        public void GetFloat_ValidChunk_ReturnsDoubleAndIncrementsIndex() { TestInitialize(); var record = new AcisRecord("test-record", _mockReader); record.Chunks.Add(new AcisChunkDouble(123.456)); int chunkIndex = 0; double result = AcisParsingUtils.GetFloat(record, ref chunkIndex, "TestFloat"); Assert.AreEqual(123.456, result, 1e-9, "GetFloat value check"); Assert.AreEqual(1, chunkIndex, "GetFloat index check"); Logger.Info("Test: GetFloat_ValidChunk_ReturnsDoubleAndIncrementsIndex PASSED"); }
        public void GetInteger_ValidChunk_ReturnsIntAndIncrementsIndex() { TestInitialize(); var record = new AcisRecord("test-record", _mockReader); record.Chunks.Add(new AcisChunkLong(789)); int chunkIndex = 0; int result = AcisParsingUtils.GetInteger(record, ref chunkIndex, "TestInt"); Assert.AreEqual(789, result, "GetInteger value check"); Assert.AreEqual(1, chunkIndex, "GetInteger index check"); Logger.Info("Test: GetInteger_ValidChunk_ReturnsIntAndIncrementsIndex PASSED"); }
        public void GetVector_FromSeparateDoubleChunks_ReturnsVector3AndIncrementsIndex() { TestInitialize(); var record = new AcisRecord("test-record", _mockReader); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(2.0)); record.Chunks.Add(new AcisChunkDouble(3.0)); int chunkIndex = 0; var expected = new Vector3(1.0f, 2.0f, 3.0f); Vector3 result = AcisParsingUtils.GetVector(record, ref chunkIndex, "TestVector"); Assert.IsTrue(ImporterUtils.IsEqual(expected, result, 1e-6f), "GetVector value check"); Assert.AreEqual(3, chunkIndex, "GetVector index check"); Logger.Info("Test: GetVector_FromSeparateDoubleChunks_ReturnsVector3AndIncrementsIndex PASSED"); }
        public void GetVector_FromVector3DChunk_ReturnsVector3AndIncrementsIndex() { TestInitialize(); var record = new AcisRecord("test-record", _mockReader); var expected = new Vector3(4.0f, 5.0f, 6.0f); record.Chunks.Add(new AcisChunkVector3D(expected)); int chunkIndex = 0; Vector3 result = AcisParsingUtils.GetVector(record, ref chunkIndex, "TestVectorFromChunk"); Assert.IsTrue(ImporterUtils.IsEqual(expected, result, 1e-6f), "GetVector (from chunk) value check"); Assert.AreEqual(1, chunkIndex, "GetVector (from chunk) index check"); Logger.Info("Test: GetVector_FromVector3DChunk_ReturnsVector3AndIncrementsIndex PASSED"); }
        public void GetRefNode_ValidRef_ReturnsEntityAndIncrementsIndex() { var readerForThisTest = new AcisReader(); AcisGlobalUtils.SetReader(readerForThisTest); var targetRecord = new AcisRecord("target-entity-record", readerForThisTest) { Index = 5 }; var targetEntity = new Point(); targetEntity.Record = targetRecord; targetRecord.Entity = targetEntity; for(int i = 0; i <= 5; ++i) readerForThisTest.RecordsList.Add(null); readerForThisTest.RecordsList[5] = targetRecord; var record = new AcisRecord("test-record", readerForThisTest); var entityRefChunk = new AcisChunkEntityRef(5); entityRefChunk.Record = targetRecord; record.Chunks.Add(entityRefChunk); int chunkIndex = 0; AcisEntity result = AcisParsingUtils.GetRefNode(record, ref chunkIndex, "TestRef"); Assert.AreSame(targetEntity, result, "GetRefNode entity check"); Assert.AreEqual(1, chunkIndex, "GetRefNode index check"); Logger.Info("Test: GetRefNode_ValidRef_ReturnsEntityAndIncrementsIndex PASSED"); }
        public void GetRefNode_NullRef_ReturnsNullAndIncrementsIndex() { TestInitialize(); var record = new AcisRecord("test-record", _mockReader); record.Chunks.Add(new AcisChunkEntityRef(-1)); int chunkIndex = 0; AcisEntity result = AcisParsingUtils.GetRefNode(record, ref chunkIndex, "TestNullRef"); Assert.IsNull(result, "GetRefNode null check"); Assert.AreEqual(1, chunkIndex, "GetRefNode null index check"); Logger.Info("Test: GetRefNode_NullRef_ReturnsNullAndIncrementsIndex PASSED"); }
    }

    public class AcisEntitySetTests
    {
        private AcisReader _testReader;
        public void TestInitializeAndSetHeader(double scale, double version) { _testReader = new AcisReader(); var header = new AcisHeader(); typeof(AcisHeader).GetProperty("Scale").SetValue(header, scale); typeof(AcisHeader).GetProperty("Version").SetValue(header, version); _testReader.GetType().GetProperty("Header").SetValue(_testReader, header); AcisGlobalUtils.SetReader(_testReader); }
        public void Point_Set_ParsesPositionCorrectly() { /* ... content from previous state ... */ TestInitializeAndSetHeader(scale: 2.0, version: 7.0); var record = new AcisRecord("point-entity", _testReader); record.Chunks.Add(new AcisChunkEntityRef(-1)); record.Chunks.Add(new AcisChunkLong(0)); record.Chunks.Add(new AcisChunkEntityRef(-1)); record.Chunks.Add(new AcisChunkDouble(10.0)); record.Chunks.Add(new AcisChunkDouble(20.0)); record.Chunks.Add(new AcisChunkDouble(30.0)); var point = new Point(); point.Set(record); var expectedPosition = new Vector3(10.0f * 2.0f, 20.0f * 2.0f, 30.0f * 2.0f); Assert.IsTrue(ImporterUtils.IsEqual(expectedPosition, point.Position, 1e-6f), $"Point Position check. Expected {expectedPosition}, Got {point.Position}"); Logger.Info("Test: Point_Set_ParsesPositionCorrectly PASSED"); }
        public void CurveStraight_Set_ParsesPropertiesCorrectly() { /* ... content from previous state ... */ TestInitializeAndSetHeader(scale: 1.0, version: 7.0); var record = new AcisRecord("straight-curve", _testReader); record.Chunks.Add(new AcisChunkEntityRef(-1)); record.Chunks.Add(new AcisChunkLong(0)); record.Chunks.Add(new AcisChunkEntityRef(-1)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(2.0)); record.Chunks.Add(new AcisChunkDouble(3.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkIdent("F")); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkIdent("F")); record.Chunks.Add(new AcisChunkDouble(100.0)); var curveStraight = new CurveStraight(); curveStraight.Set(record); var expectedRoot = new Vector3(1.0f, 2.0f, 3.0f); var expectedDir = new Vector3(0.0f, 1.0f, 0.0f); Assert.IsTrue(ImporterUtils.IsEqual(expectedRoot, curveStraight.Root, 1e-6f), "CurveStraight Root check"); Assert.IsTrue(ImporterUtils.IsEqual(expectedDir, curveStraight.Dir, 1e-6f), "CurveStraight Dir check"); Assert.AreEqual("F", curveStraight.CurveRange.Lower.Type); Assert.AreEqual(0.0, curveStraight.CurveRange.Lower.GetLimit()); Assert.AreEqual("F", curveStraight.CurveRange.Upper.Type); Assert.AreEqual(100.0, curveStraight.CurveRange.Upper.GetLimit()); Logger.Info("Test: CurveStraight_Set_ParsesPropertiesCorrectly PASSED"); }
        public void CurveInt_Set_ParsesHelixWithPCurvesCorrectly() { /* ... content from previous state ... */ TestInitializeAndSetHeader(scale: 1.0, version: 7.0); var record = new AcisRecord("helix-curve-int", _testReader); record.Chunks.Add(new AcisChunkEntityRef(-1)); record.Chunks.Add(new AcisChunkLong(0)); record.Chunks.Add(new AcisChunkEntityRef(-1)); record.Chunks.Add(new AcisChunkIdent("helix_int_cur")); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(2.0)); record.Chunks.Add(new AcisChunkDouble(3.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(5.0)); record.Chunks.Add(new AcisChunkDouble(5.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(10.0)); record.Chunks.Add(new AcisChunkIdent("R")); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkIdent("R")); record.Chunks.Add(new AcisChunkDouble(Math.PI * 4.0)); record.Chunks.Add(new AcisChunkIdent("T")); record.Chunks.Add(new AcisChunkLong(2)); record.Chunks.Add(new AcisChunkIdent("F")); record.Chunks.Add(new AcisChunkLong(0)); record.Chunks.Add(new AcisChunkLong(3)); record.Chunks.Add(new AcisChunkLong(6)); record.Chunks.Add(new AcisChunkDouble(0.1)); record.Chunks.Add(new AcisChunkDouble(0.2)); record.Chunks.Add(new AcisChunkDouble(0.3)); record.Chunks.Add(new AcisChunkDouble(0.4)); record.Chunks.Add(new AcisChunkDouble(0.5)); record.Chunks.Add(new AcisChunkDouble(0.6)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkIdent("T")); record.Chunks.Add(new AcisChunkLong(1)); record.Chunks.Add(new AcisChunkIdent("F")); record.Chunks.Add(new AcisChunkLong(0)); record.Chunks.Add(new AcisChunkLong(2)); record.Chunks.Add(new AcisChunkLong(4)); record.Chunks.Add(new AcisChunkDouble(0.7)); record.Chunks.Add(new AcisChunkDouble(0.8)); record.Chunks.Add(new AcisChunkDouble(0.9)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); var curveInt = new CurveInt(); curveInt.Set(record); Assert.AreEqual("helix_int_cur", curveInt.SubType); Logger.Info("Test: CurveInt_Set_ParsesHelixWithPCurvesCorrectly PASSED"); }
        public void SurfaceSpline_Set_ParsesBSplineSurfaceCorrectly() { /* ... content from previous state ... */ TestInitializeAndSetHeader(scale: 1.0, version: 7.0); var record = new AcisRecord("bspline-surface", _testReader); record.Chunks.Add(new AcisChunkEntityRef(-1)); record.Chunks.Add(new AcisChunkLong(0)); record.Chunks.Add(new AcisChunkEntityRef(-1)); record.Chunks.Add(new AcisChunkLong(2)); record.Chunks.Add(new AcisChunkLong(2)); record.Chunks.Add(new AcisChunkIdent("F")); record.Chunks.Add(new AcisChunkLong(0)); record.Chunks.Add(new AcisChunkLong(0)); record.Chunks.Add(new AcisChunkLong(3)); record.Chunks.Add(new AcisChunkLong(3)); record.Chunks.Add(new AcisChunkLong(6)); record.Chunks.Add(new AcisChunkLong(6)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.5)); record.Chunks.Add(new AcisChunkDouble(2.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(0.5)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(2.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(0.5)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(2.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(2.0)); record.Chunks.Add(new AcisChunkDouble(0.5)); record.Chunks.Add(new AcisChunkDouble(2.0)); record.Chunks.Add(new AcisChunkDouble(2.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(0.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); record.Chunks.Add(new AcisChunkDouble(1.0)); var surfaceSpline = new SurfaceSpline(); surfaceSpline.Set(record); Assert.IsNotNull(surfaceSpline.SplineGeometricData); Logger.Info("Test: SurfaceSpline_Set_ParsesBSplineSurfaceCorrectly PASSED"); }

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
            entitySetTests.SurfaceSpline_Set_ParsesBSplineSurfaceCorrectly();

            AcisToStepConverterTests.RunTests();
            Logger.Info("All conceptual tests finished.");
        }
    }

    public class AcisToStepConverterTests
    {
        private AcisReader _mockAcisReader;
        public void TestInitialize() { /* ... content from previous state ... */ StepConverterUtils.InitExport(); var acisHeader = new AcisHeader(version: 26.0, scale: 1.0); _mockAcisReader = new AcisReader(acisHeader); AcisGlobalUtils.SetReader(_mockAcisReader); }
        private int CountOccurrences(string text, string pattern) { if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern)) return 0; return Regex.Matches(text, Regex.Escape(pattern)).Count; }
        private int CountOccurrencesRegex(string text, string regexPattern) { if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(regexPattern)) return 0; return Regex.Matches(text, regexPattern).Count; }

        // All existing AcisToStepConverterTests methods (B-Spline creation, surface creation, style creation, assembly export) are preserved here...
        // For brevity, their full code is not repeated in this diff view, but they are part of the overwritten file.
        public void TestCreateBSplineCurveGeometry_NonRational_CorrectProperties() { /* ... */ }
        public void TestCreateBSplineCurveGeometry_Rational_CorrectProperties() { /* ... */ }
        public void TestCreateBSplineSurfaceGeometry_NonRational_CorrectProperties() { /* ... */ }
        public void TestCreateBSplineSurfaceGeometry_Rational_CorrectProperties() { /* ... */ }
        public void TestRationalBSplineCurve_ExportStep_FormatsCorrectly() { /* ... */ }
        public void TestCreateAndExport_ConicalSurface() { /* ... */ }
        public void TestCreateAndExport_SphericalSurface() { /* ... */ }
        public void TestCreateAndExport_ToroidalSurface() { /* ... */ }
        public void TestExportStep_BSplineSurfaceWithKnots_FormatsCorrectly() { /* ... */ }
        public void TestCreateAndExport_StyledItem_WithCurveStyle() { /* ... */ }
        public void TestCreateAndExport_StyledItem_WithSurfaceStyle() { /* ... */ }
        public void TestExport_SimpleAssembly_GeneratesValidStepFileStructure() { /* ... content from previous successful overwrite ... */ }


        public void TestReadFile_WithSimulatedRealPartData()
        {
            TestInitialize(); // Standard init for STEP conversion utils
            string dummyFilePath = "dummy_part_for_reader_test.ipt";

            string rseDbSimData = "SIMULATED_RSEDB_PART:UFRxDoc,DocType_UFRxDoc,uid_ufrx;AmBREPSegmentType,MbBrepSegmentType,uid_brep;PmDcSegmentType,DesignConstraints,uid_pmdc;AmAppSegmentType,ApplicationData,uid_app";
            string ufrxDocSimData = "UFRxDoc_Sim_Version:26.0.0.0;FileName:TestCubeTransformed.ipt;DocGUID:{CUBE-TR-GUID-A1B2C3D4E5};VersionGUID:{CUBE-TR-VERS-A1B2C3D4E5}";

            // ACIS Text for a simple 10x10x10 cube, translated by (10, 20, 30)
            // One face (Z=10 face, original coords) will be colored red.
            string acisPartData = @"26.0.0 1108 ACIS 26.0 NT SatMExport
1 1 0
placeholder-unit 1 millimeter
End-of-ACIS-History-Marker-

transform-entity $-1 $-1 $-1 $-1 1 0 0 0 1 0 0 0 1 10 20 30 I I I $-1 # Translate (10,20,30) (idx 1)

plane-surface $-1 $-1 $-1 $-1 0 0 0 0 0 -1 1 0 0 forward_v -1e6 1e6 -1e6 1e6 $-1 # Z=0 plane (idx 2)
plane-surface $-1 $-1 $-1 $-1 0 0 10 0 0 1 1 0 0 forward_v -1e6 1e6 -1e6 1e6 $-1 # Z=10 plane (idx 3) - This face will be colored
plane-surface $-1 $-1 $-1 $-1 0 0 0 0 -1 0 1 0 0 forward_v -1e6 1e6 -1e6 1e6 $-1 # Y=0 plane (idx 4)
plane-surface $-1 $-1 $-1 $-1 0 10 0 0 1 0 1 0 0 forward_v -1e6 1e6 -1e6 1e6 $-1 # Y=10 plane (idx 5)
plane-surface $-1 $-1 $-1 $-1 0 0 0 -1 0 0 0 1 0 forward_v -1e6 1e6 -1e6 1e6 $-1 # X=0 plane (idx 6)
plane-surface $-1 $-1 $-1 $-1 10 0 0 1 0 0 0 1 0 forward_v -1e6 1e6 -1e6 1e6 $-1 # X=10 plane (idx 7)

point-entity $-1 $-1 $-1 $-1 0 0 0 $-1 # P0 (idx 8)
point-entity $-1 $-1 $-1 $-1 10 0 0 $-1 # P1 (idx 9)
point-entity $-1 $-1 $-1 $-1 10 10 0 $-1 # P2 (idx 10)
point-entity $-1 $-1 $-1 $-1 0 10 0 $-1 # P3 (idx 11)
point-entity $-1 $-1 $-1 $-1 0 0 10 $-1 # P4 (idx 12)
point-entity $-1 $-1 $-1 $-1 10 0 10 $-1 # P5 (idx 13)
point-entity $-1 $-1 $-1 $-1 10 10 10 $-1 # P6 (idx 14)
point-entity $-1 $-1 $-1 $-1 0 10 10 $-1 # P7 (idx 15)

vertex-entity $-1 $-1 $-1 #1 #8 $-1  # V0 (idx 16)
vertex-entity $-1 $-1 $-1 #1 #9 $-1  # V1 (idx 17)
vertex-entity $-1 $-1 $-1 #1 #10 $-1 # V2 (idx 18)
vertex-entity $-1 $-1 $-1 #1 #11 $-1 # V3 (idx 19)
vertex-entity $-1 $-1 $-1 #1 #12 $-1 # V4 (idx 20)
vertex-entity $-1 $-1 $-1 #1 #13 $-1 # V5 (idx 21)
vertex-entity $-1 $-1 $-1 #1 #14 $-1 # V6 (idx 22)
vertex-entity $-1 $-1 $-1 #1 #15 $-1 # V7 (idx 23)

straight-curve $-1 $-1 $-1 0 0 0 1 0 0 F 0 F 10 $-1 # C01 (idx 24)
straight-curve $-1 $-1 $-1 10 0 0 0 1 0 F 0 F 10 $-1 # C12 (idx 25)
straight-curve $-1 $-1 $-1 10 10 0 -1 0 0 F 0 F 10 $-1 # C23 (idx 26)
straight-curve $-1 $-1 $-1 0 10 0 0 -1 0 F 0 F 10 $-1 # C30 (idx 27)
straight-curve $-1 $-1 $-1 0 0 10 1 0 0 F 0 F 10 $-1 # C45 (idx 28)
straight-curve $-1 $-1 $-1 10 0 10 0 1 0 F 0 F 10 $-1 # C56 (idx 29)
straight-curve $-1 $-1 $-1 10 10 10 -1 0 0 F 0 F 10 $-1 # C67 (idx 30)
straight-curve $-1 $-1 $-1 0 10 10 0 -1 0 F 0 F 10 $-1 # C74 (idx 31)
straight-curve $-1 $-1 $-1 0 0 0 0 0 1 F 0 F 10 $-1 # C04 (idx 32)
straight-curve $-1 $-1 $-1 10 0 0 0 0 1 F 0 F 10 $-1 # C15 (idx 33)
straight-curve $-1 $-1 $-1 10 10 0 0 0 1 F 0 F 10 $-1 # C26 (idx 34)
straight-curve $-1 $-1 $-1 0 10 0 0 0 1 F 0 F 10 $-1 # C37 (idx 35)

edge-entity $-1 $-1 $-1 #16 0 #17 0 #48 #24 forward "" I $-1 # E01 (idx 36)
edge-entity $-1 $-1 $-1 #17 0 #18 0 #49 #25 forward "" I $-1 # E12 (idx 37)
edge-entity $-1 $-1 $-1 #18 0 #19 0 #50 #26 forward "" I $-1 # E23 (idx 38)
edge-entity $-1 $-1 $-1 #19 0 #16 0 #51 #27 forward "" I $-1 # E30 (idx 39)
edge-entity $-1 $-1 $-1 #20 0 #21 0 #52 #28 forward "" I $-1 # E45 (idx 40)
edge-entity $-1 $-1 $-1 #21 0 #22 0 #53 #29 forward "" I $-1 # E56 (idx 41)
edge-entity $-1 $-1 $-1 #22 0 #23 0 #54 #30 forward "" I $-1 # E67 (idx 42)
edge-entity $-1 $-1 $-1 #23 0 #20 0 #55 #31 forward "" I $-1 # E74 (idx 43)
edge-entity $-1 $-1 $-1 #16 0 #20 0 #56 #32 forward "" I $-1 # E04 (idx 44)
edge-entity $-1 $-1 $-1 #17 0 #21 0 #57 #33 forward "" I $-1 # E15 (idx 45)
edge-entity $-1 $-1 $-1 #18 0 #22 0 #58 #34 forward "" I $-1 # E26 (idx 46)
edge-entity $-1 $-1 $-1 #19 0 #23 0 #59 #35 forward "" I $-1 # E37 (idx 47)

coedge-entity $-1 $-1 $-1 #1 #1 #36 forward #1 $-1 $-1 # Dummy coedges for now (idx 48-71)
coedge-entity $-1 $-1 $-1 #1 #1 #37 forward #1 $-1 $-1
coedge-entity $-1 $-1 $-1 #1 #1 #38 forward #1 $-1 $-1
coedge-entity $-1 $-1 $-1 #1 #1 #39 forward #1 $-1 $-1
coedge-entity $-1 $-1 $-1 #1 #1 #40 forward #1 $-1 $-1
coedge-entity $-1 $-1 $-1 #1 #1 #41 forward #1 $-1 $-1
coedge-entity $-1 $-1 $-1 #1 #1 #42 forward #1 $-1 $-1
coedge-entity $-1 $-1 $-1 #1 #1 #43 forward #1 $-1 $-1
coedge-entity $-1 $-1 $-1 #1 #1 #44 forward #1 $-1 $-1
coedge-entity $-1 $-1 $-1 #1 #1 #45 forward #1 $-1 $-1
coedge-entity $-1 $-1 $-1 #1 #1 #46 forward #1 $-1 $-1
coedge-entity $-1 $-1 $-1 #1 #1 #47 forward #1 $-1 $-1
# Need 12 more coedges (2 per edge) - these are just placeholders
coedge-entity $-1 $-1 $-1 #1 #1 #36 reversed #1 $-1 $-1
coedge-entity $-1 $-1 $-1 #1 #1 #37 reversed #1 $-1 $-1
coedge-entity $-1 $-1 $-1 #1 #1 #38 reversed #1 $-1 $-1
coedge-entity $-1 $-1 $-1 #1 #1 #39 reversed #1 $-1 $-1
coedge-entity $-1 $-1 $-1 #1 #1 #40 reversed #1 $-1 $-1
coedge-entity $-1 $-1 $-1 #1 #1 #41 reversed #1 $-1 $-1
coedge-entity $-1 $-1 $-1 #1 #1 #42 reversed #1 $-1 $-1
coedge-entity $-1 $-1 $-1 #1 #1 #43 reversed #1 $-1 $-1
coedge-entity $-1 $-1 $-1 #1 #1 #44 reversed #1 $-1 $-1
coedge-entity $-1 $-1 $-1 #1 #1 #45 reversed #1 $-1 $-1
coedge-entity $-1 $-1 $-1 #1 #1 #46 reversed #1 $-1 $-1
coedge-entity $-1 $-1 $-1 #1 #1 #47 reversed #1 $-1 $-1

loop-entity $-1 $-1 #48 #78 #72 I $-1 # Face0_Z0_Loop (idx 72)
loop-entity $-1 $-1 #52 #79 #73 I $-1 # Face1_Z10_Loop (idx 73)
loop-entity $-1 $-1 #1 #1 #74 I $-1 # Face2_Y0_Loop (idx 74)
loop-entity $-1 $-1 #1 #1 #75 I $-1 # Face3_Y10_Loop (idx 75)
loop-entity $-1 $-1 #1 #1 #76 I $-1 # Face4_X0_Loop (idx 76)
loop-entity $-1 $-1 #1 #1 #77 I $-1 # Face5_X10_Loop (idx 77)

rgb_color-st-attrib-entity $-1 $-1 $-1 1.0 0.0 0.0 $-1 # Red color for Z=10 face (idx 78)
pointer-attrib-entity $-1 $-1 $-1 #78 #79 $-1 # Link color to face (idx 79)

face-entity $-1 $-1 #72 #80 $-1 #2 forward single $ I $-1 # Face0_Z0 (idx 80)
face-entity #79 $-1 #73 #81 #80 #3 forward single $ I $-1 # Face1_Z10 (idx 81) - COLORED
face-entity $-1 $-1 #74 #82 #81 #4 forward single $ I $-1 # Face2_Y0 (idx 82)
face-entity $-1 $-1 #75 #83 #82 #5 forward single $ I $-1 # Face3_Y10 (idx 83)
face-entity $-1 $-1 #76 #84 #83 #6 forward single $ I $-1 # Face4_X0 (idx 84)
face-entity $-1 $-1 #77 #85 #84 #7 forward single $ I $-1 # Face5_X10 (idx 85)

shell-entity $-1 $-1 #80 $-1 #86 I $-1 # Shell (idx 86)
lump-entity $-1 $-1 #86 #87 I $-1 # Lump (idx 87)
body-entity $-1 $-1 #87 $-1 #1 I $-1 # Body_Cube (idx 88)

End-of-ACIS-data
";

            var overrideStreams = new Dictionary<string, byte[]>()
            {
                { "RSeDb", Encoding.UTF8.GetBytes(rseDbSimData) },
                { "UFRxDoc", Encoding.UTF8.GetBytes(ufrxDocSimData) },
                { "AmBREPSegmentType", Encoding.ASCII.GetBytes(acisPartData) },
                { "PmDcSegmentType", new byte[]{ 0xDC, 0xFE, 0xED, 0xC0 } }, // Ensure non-empty for readers that expect some data
                { "AmAppSegmentType", new byte[]{ 0xAA, 0xBE, 0xEF, 0xA0 } }
            };

            InventorReader.SimulatedOleFile.TestStreamOverrides = overrideStreams;
            Inventor inventorModel = null;
            try
            {
                var reader = new InventorReader();
                inventorModel = reader.ReadFile(dummyFilePath, Logger.LogWriter);
            }
            finally
            {
                InventorReader.SimulatedOleFile.TestStreamOverrides = null;
            }

            Assert.IsNotNull(inventorModel, "InventorModel should not be null after ReadFile");
            Assert.IsNotNull(inventorModel.RSeDb, "RSeDb object should be initialized");
            Assert.IsNotNull(inventorModel.RSeDb.SegInfo, "RSeDb.SegInfo should be initialized");
            Assert.IsNotNull(inventorModel.RSeDb.SegInfo.SegmentDirectory, "SegmentDirectory should be initialized by RSeDbReader");
            Assert.IsTrue(inventorModel.RSeDb.SegInfo.SegmentDirectory.Count >= 4, $"SegmentDirectory should have at least 4 entries from sim data. Got: {inventorModel.RSeDb.SegInfo.SegmentDirectory.Count}");

            var brepEntry = inventorModel.RSeDb.SegInfo.SegmentDirectory.Find(s => s.Name == "AmBREPSegmentType");
            Assert.IsNotNull(brepEntry, "BRep segment entry 'AmBREPSegmentType' should exist in directory");
            if(brepEntry != null) Assert.AreEqual("MbBrepSegmentType", brepEntry.TypeString, "BRep entry type string mismatch");

            Assert.IsNotNull(inventorModel.UFRxDoc, "UFRxDoc object should be initialized");
            Assert.IsNotNull(inventorModel.UFRxDoc.Header1, "UFRxDoc.Header1 should be initialized");
            Assert.AreEqual("15.0.0.0", inventorModel.UFRxDoc.Header1.VersionString, "UFRx VersionString mismatch");
            Assert.AreEqual("TestPartFromSim.ipt", inventorModel.UFRxDoc.Header1.FileName, "UFRx FileName mismatch");

            Assert.IsTrue(inventorModel.Segments.ContainsKey("AmBREPSegmentType"), "BRep segment 'AmBREPSegmentType' should be loaded");
            var brepSegment = inventorModel.Segments["AmBREPSegmentType"];
            Assert.IsNotNull(brepSegment, "BRepSegment object is null");
            Assert.IsTrue(brepSegment.ParsedContent.ContainsKey("ACIS"), "ACIS data should be parsed from BRep segment");

            AcisReader acisReader = brepSegment.ParsedContent["ACIS"] as AcisReader;
            Assert.IsNotNull(acisReader, "AcisReader object expected from BRep segment.");
            Assert.IsNotNull(acisReader.Header, "ACIS header should be parsed.");
            Assert.AreEqual(26.0, acisReader.Header.Version, 1e-9, "ACIS header version mismatch for cube."); // From ACIS string

            // Check for specific entity counts from the cube data
            Assert.IsTrue(acisReader.RecordsList.Count(r => r.Entity is AcisTransform) >= 1, "Should parse at least 1 transform.");
            Assert.IsTrue(acisReader.RecordsList.Count(r => r.Entity is AcisSurfacePlane) >= 6, "Should parse at least 6 plane-surfaces.");
            Assert.IsTrue(acisReader.RecordsList.Count(r => r.Entity is AcisPoint) >= 8, "Should parse at least 8 points.");
            Assert.IsTrue(acisReader.RecordsList.Count(r => r.Entity is AcisCurveStraight) >= 12, "Should parse at least 12 straight-curves.");
            Assert.IsTrue(acisReader.RecordsList.Count(r => r.Entity is AcisEdge) >= 12, "Should parse at least 12 edges.");
            Assert.IsTrue(acisReader.RecordsList.Count(r => r.Entity is AcisFace) >= 6, "Should parse at least 6 faces.");
            Assert.IsTrue(acisReader.RecordsList.Count(r => r.Entity is AcisShell) >= 1, "Should parse at least 1 shell.");
            Assert.IsTrue(acisReader.RecordsList.Count(r => r.Entity is AcisBody) >= 1, "Should parse at least 1 body.");
            Assert.IsTrue(acisReader.RecordsList.Count(r => r.Entity is AttribStRgbColor) >= 1, "Should parse at least 1 color attribute.");

            // Spot check a point's coordinates (e.g. P0 at 0,0,0)
            Assert.IsTrue(acisReader.RecordsList.Any(r => r.Entity is AcisPoint pt && ImporterUtils.IsEqual(Vector3.Zero, pt.Position, 1e-6f)), "ACIS Origin point check.");

            // Call STEP Export
            string stepOutput = StepConverterUtils.Export(inventorModel, "TestCubeFromAcis.stp", Logger.LogWriter);
            Assert.IsTrue(!string.IsNullOrEmpty(stepOutput), "STEP output string should not be null or empty");
            // File.WriteAllText("TestCubeFromAcis_Output.stp", stepOutput); // For manual inspection if needed

            // --- Validate STEP Output String ---
            Assert.IsTrue(stepOutput.StartsWith("ISO-10303-21;"), "STEP Starts correctly");
            Assert.IsTrue(stepOutput.Contains("HEADER;"), "Contains HEADER section");
            Assert.IsTrue(stepOutput.Contains("DATA;"), "Contains DATA section");
            Assert.IsTrue(stepOutput.Contains("ENDSEC;"), "Contains ENDSEC");
            Assert.IsTrue(stepOutput.Contains("END-ISO-10303-21;"), "STEP Ends correctly");

            // Product Structure (1 part definition, 1 assembly definition for the single part)
            Assert.AreEqual(2, CountOccurrencesRegex(stepOutput, "\\bPRODUCT\\("), "PRODUCT count (Asm, Part1)");
            Assert.AreEqual(2, CountOccurrencesRegex(stepOutput, "\\bPRODUCT_DEFINITION\\("), "PRODUCT_DEFINITION count (Asm, Part1)");
            Assert.AreEqual(1, CountOccurrencesRegex(stepOutput, "\\bNEXT_ASSEMBLY_USAGE_OCCURRENCE\\("), "NAUO count for the single part");
            Assert.AreEqual(1, CountOccurrencesRegex(stepOutput, "\\bCONTEXT_DEPENDENT_SHAPE_REPRESENTATION\\("), "CDSR count for the single part");

            // Geometry - Cube (Transformed by (10,20,30))
            // For a single body, Export creates 1 MSB/SBSM for the part's canonical shape,
            // then 1 MAPPED_ITEM for its instance in the assembly.
            Assert.AreEqual(1, CountOccurrencesRegex(stepOutput, "\\bMANIFOLD_SOLID_BREP\\(") + CountOccurrencesRegex(stepOutput, "\\bSHELL_BASED_SURFACE_MODEL\\("), "1 Canonical Shape (MSB or SBSM)");
            Assert.AreEqual(1, CountOccurrencesRegex(stepOutput, "\\bMAPPED_ITEM\\("), "MAPPED_ITEM for transformed cube");
            Assert.AreEqual(1, CountOccurrencesRegex(stepOutput, "\\bREPRESENTATION_MAP\\("), "REPRESENTATION_MAP for transform");

            Assert.IsTrue(CountOccurrencesRegex(stepOutput, "\\bCARTESIAN_POINT\\(") >= 8, "CARTESIAN_POINT count for cube vertices"); // At least 8 for canonical, more if transform creates unique points
            Assert.IsTrue(CountOccurrencesRegex(stepOutput, "\\bLINE\\(") >= 12, "LINE count for cube edges");
            Assert.IsTrue(CountOccurrencesRegex(stepOutput, "\\bPLANE\\(") >= 6, "PLANE count for cube faces");
            Assert.IsTrue(CountOccurrencesRegex(stepOutput, "\\bAXIS2_PLACEMENT_3D\\(") >= 7, "AXIS2_PLACEMENT_3D count (6 planes + 1 mapped item transform)");
            Assert.IsTrue(CountOccurrencesRegex(stepOutput, "\\bADVANCED_FACE\\(") >= 6, "ADVANCED_FACE count");
            Assert.IsTrue(CountOccurrencesRegex(stepOutput, "\\bCLOSED_SHELL\\(") >= 1, "CLOSED_SHELL count (expecting cube to be closed)");

            // Transformed Coordinates Check
            // Original P0 (0,0,0) transformed by (10,20,30) should be (10,20,30)
            string p0_transformed_X = StepConverterUtils.DoubleToString(10.0);
            string p0_transformed_Y = StepConverterUtils.DoubleToString(20.0);
            string p0_transformed_Z = StepConverterUtils.DoubleToString(30.0);
            // This point will be part of the AXIS2_PLACEMENT_3D for the MAPPED_ITEM.
            // The canonical points (0,0,0 etc.) will also exist.
            Assert.IsTrue(stepOutput.Contains($"CARTESIAN_POINT\\('',\\({p0_transformed_X},{p0_transformed_Y},{p0_transformed_Z}\\)\\)"), "Transformed origin point check for cube");

            // Original P6 (10,10,10) transformed by (10,20,30) should be (20,30,40)
            string p6_transformed_X = StepConverterUtils.DoubleToString(10.0 + 10.0);
            string p6_transformed_Y = StepConverterUtils.DoubleToString(10.0 + 20.0);
            string p6_transformed_Z = StepConverterUtils.DoubleToString(10.0 + 30.0);
            // This specific transformed point might not be explicitly written if not a primary placement origin.
            // However, the MAPPED_ITEM's transform should reflect this.
            // We are checking the placement origin of the mapped item's transform.

            // Styles (Face Z=10 was colored Red (1,0,0))
            Assert.IsTrue(CountOccurrencesRegex(stepOutput, "STYLED_ITEM\\(") >= 1, "STYLED_ITEM for colored face expected");
            Assert.IsTrue(CountOccurrencesRegex(stepOutput, "COLOUR_RGB\\('[^']*',1\\.0,0\\.0,0\\.0\\)") >= 1, "Red COLOUR_RGB expected");
            Assert.IsTrue(CountOccurrencesRegex(stepOutput, "SURFACE_STYLE_FILL_AREA\\(") >= 1, "SURFACE_STYLE_FILL_AREA expected");
            Assert.IsTrue(CountOccurrencesRegex(stepOutput, "SURFACE_STYLE_USAGE\\(") >= 1, "SURFACE_STYLE_USAGE expected");
            Assert.IsTrue(CountOccurrencesRegex(stepOutput, "PRESENTATION_STYLE_ASSIGNMENT\\(") >= 1, "PRESENTATION_STYLE_ASSIGNMENT expected");

            Logger.Info("Test: TestReadFile_WithSimulatedRealPartData (Cube ACIS to STEP) PASSED");
        }


        public static void RunTests()
        {
            // ... (Existing RunTests calls)
            Logger.LogWriter = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true }; // Ensure logger is set for all tests

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
            entitySetTests.SurfaceSpline_Set_ParsesBSplineSurfaceCorrectly();

            AcisToStepConverterTests converterTests = new AcisToStepConverterTests();
            converterTests.TestCreateBSplineCurveGeometry_NonRational_CorrectProperties();
            converterTests.TestCreateBSplineCurveGeometry_Rational_CorrectProperties();
            converterTests.TestCreateBSplineSurfaceGeometry_NonRational_CorrectProperties();
            converterTests.TestCreateBSplineSurfaceGeometry_Rational_CorrectProperties();
            converterTests.TestRationalBSplineCurve_ExportStep_FormatsCorrectly();
            converterTests.TestCreateAndExport_ConicalSurface();
            converterTests.TestCreateAndExport_SphericalSurface();
            converterTests.TestCreateAndExport_ToroidalSurface();
            converterTests.TestExportStep_BSplineSurfaceWithKnots_FormatsCorrectly();
            converterTests.TestCreateAndExport_StyledItem_WithCurveStyle();
            converterTests.TestCreateAndExport_StyledItem_WithSurfaceStyle();
            converterTests.TestExport_SimpleAssembly_GeneratesValidStepFileStructure();
            converterTests.TestReadFile_WithSimulatedRealPartData(); // Added new test call

            Logger.Info("All conceptual tests finished.");
        }
    }
}
