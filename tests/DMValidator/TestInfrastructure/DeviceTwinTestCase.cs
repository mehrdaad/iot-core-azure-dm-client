﻿/*
Copyright 2018 Microsoft
Permission is hereby granted, free of charge, to any person obtaining a copy of this software 
and associated documentation files (the "Software"), to deal in the Software without restriction, 
including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, 
subject to the following conditions:

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT 
LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, 
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH 
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DMValidator
{
    class DeviceTwinTestCase : TestCase
    {
        public override void Dump()
        {
            Debug.WriteLine("Name           : " + _name);
            Debug.WriteLine("Desription     : " + _description);
            Debug.WriteLine("Input          : " + _desiredState.ToString());
            if (_expectedPresentReportedState != null)
            {
                Debug.WriteLine("Expected Present Output: " + _expectedPresentReportedState.ToString());
            }
            else
            {
                Debug.WriteLine("Expected Present Output: null");
            }
            if (_expectedAbsentReportedState != null)
            {
                Debug.WriteLine("Expected Absent Output: " + _expectedAbsentReportedState.ToString());
            }
            else
            {
                Debug.WriteLine("Expected Absent Output: null");
            }
        }

        public static DeviceTwinTestCase FromJson(ILogger logger, JObject testCaseJson)
        {
            string name;
            if (!JsonHelpers.TryGetString(testCaseJson, Constants.TCJsonName, out name))
            {
                ReportError(logger, "Missing " + Constants.TCJsonName);
                return null;
            }

            string description;
            if (!JsonHelpers.TryGetString(testCaseJson, Constants.TCJsonDescription, out description))
            {
                ReportError(logger, "Missing " + Constants.TCJsonDescription);
                return null;
            }

            JObject input;
            if (!JsonHelpers.TryGetObject(testCaseJson, Constants.TCJsonInput, out input))
            {
                ReportError(logger, "Missing " + Constants.TCJsonInput);
                return null;
            }

            JObject output;
            if (!JsonHelpers.TryGetObject(testCaseJson, Constants.TCJsonOutput, out output))
            {
                ReportError(logger, "Missing " + Constants.TCJsonOutput);
                return null;
            }

            JObject expectedPresentReportedState = null;
            if (JsonHelpers.TryGetObject(output, Constants.TCJsonOutputPresent, out expectedPresentReportedState))
            {
                expectedPresentReportedState = (JObject)expectedPresentReportedState.DeepClone();
            }

            JObject expectedAbsentReportedState = null;
            if (JsonHelpers.TryGetObject(output, Constants.TCJsonOutputAbsent, out expectedAbsentReportedState))
            {
                expectedAbsentReportedState = (JObject)expectedAbsentReportedState.DeepClone();
            }

            DeviceTwinTestCase testCase = new DeviceTwinTestCase();
            testCase._name = name;
            testCase._description = description;
            testCase._desiredState = input;
            testCase._expectedPresentReportedState = expectedPresentReportedState;
            testCase._expectedAbsentReportedState = expectedAbsentReportedState;
            return testCase;
        }

        public override async Task<bool> Execute(ILogger logger, IoTHubManager client, TestParameters testParameters)
        {

            JObject resolvedDesiredState = (JObject)testParameters.ResolveParameters(_desiredState);
            JToken desiredNode = resolvedDesiredState[Constants.JsonPropertiesRoot][Constants.JsonDesiredRoot];
            if (desiredNode is JObject)
            {
                JObject desiredJObject = (JObject)desiredNode;
                await client.UpdateDesiredProperties(testParameters.IoTHubDeviceId, Constants.JsonDesiredRoot, desiredJObject);
            }
            else
            {
                throw new Exception("Unexpected format!");
            }

            int seconds = 15;
            logger.Log(LogLevel.Information, "Waiting " + seconds + " seconds for the device twin to be updated...");
            await Task.Delay(seconds * 1000);

            DeviceData deviceData = await client.GetDeviceData(testParameters.IoTHubDeviceId);

            JObject desiredProperties = (JObject)JsonConvert.DeserializeObject(deviceData.desiredPropertiesJson);
            JObject reportedProperties = (JObject)JsonConvert.DeserializeObject(deviceData.reportedPropertiesJson);

            logger.Log(LogLevel.Verbose, "---- Final Result:");
            logger.Log(LogLevel.Verbose, reportedProperties.ToString());

            JObject expectedWindowsReported = (JObject)_expectedPresentReportedState[Constants.JsonPropertiesRoot][Constants.JsonReportedRoot];

            List<string> errorList = new List<string>();
            bool result = true;

            logger.Log(LogLevel.Verbose, "---- Expected Present Result:");
            if (expectedWindowsReported != null)
            {
                logger.Log(LogLevel.Verbose, expectedWindowsReported.ToString());
                result &= TestCaseHelpers.VerifyPropertiesPresent(Constants.JsonDeviceTwin, expectedWindowsReported, reportedProperties, errorList);
            }

            logger.Log(LogLevel.Verbose, "---- Expected Absent Result:");
            if (_expectedAbsentReportedState != null)
            {
                JObject expectedAbsentReported = (JObject)_expectedAbsentReportedState[Constants.JsonPropertiesRoot][Constants.JsonReportedRoot];
                if (expectedAbsentReported != null)
                {
                    logger.Log(LogLevel.Verbose, expectedAbsentReported.ToString());
                    result &= TestCaseHelpers.VerifyPropertiesAbsent(expectedAbsentReported, reportedProperties, errorList);
                }
            }

            ReportResult(logger, result, errorList);

            return result;
        }

        protected JObject _desiredState;
        protected JObject _expectedPresentReportedState;
        protected JObject _expectedAbsentReportedState;
    }
}