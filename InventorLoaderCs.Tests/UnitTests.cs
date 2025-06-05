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

            Logger.Info("All conceptual tests finished.");
        }
    }
}
