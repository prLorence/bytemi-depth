namespace UnityEngine.XR.ARFoundation.Samples.Assets.Scripts.Runtime
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    [Serializable]
    public class NutritionApiResponse
    {
        public ResponseData data;
        public MacronutrientsData macronutrients;
        public bool success;
    }

    [Serializable]
    public class ResponseData
    {
        public string frame_id;
        public List<VolumeData> volumes;
    }

    [Serializable]
    public class VolumeData
    {
        public string object_name;
        public float uncertainty_cups;
        public float volume_cups;
    }

    [Serializable]
    public class MacronutrientsData
    {
        public List<FoodMacroData> data;
    }

    [Serializable]
    public class FoodMacroData
    {
        public string ServingInfo;
        public bool found;
        public MacroNutrients macros;
        public string requested_food;
        public float volume;
        public float calculated_weight;
    }

    [Serializable]
    public class MacroNutrients
    {
        public float calories;
        public float carbs;
        public float fat;
        public float protein;
    }

    // Display-friendly data structure
    [Serializable]
    public class FoodNutritionInfo
    {
        public string foodName;
        public float fat;
        public float protein;
        public float carbohydrates;
        public float estimatedVolume;
        public string volumeUnit = "cups";
        public float calculatedWeight;
        public string weightUnit = "g";

        public static FoodNutritionInfo FromApiData(ResponseData responseData, FoodMacroData macroData)
        {
            // Find matching volume data
            var volumeData = responseData.volumes.Find(v => v.object_name == macroData.requested_food);

            // Parse weight from ServingInfo
            return new FoodNutritionInfo
            {
                foodName = macroData.requested_food,
                fat = macroData.macros.fat,
                protein = macroData.macros.protein,
                carbohydrates = macroData.macros.carbs,
                estimatedVolume = macroData.volume,
                calculatedWeight = macroData.calculated_weight
            };
        }
    }

    // Test data generator
    public static class NutritionTestData
    {
        public static NutritionApiResponse CreateDummyResponse()
        {
            return new NutritionApiResponse
            {
                data = new ResponseData
                {
                    frame_id = "130043.51440302501",
                    volumes = new List<VolumeData>
                {
                    new VolumeData
                    {
                        object_name = "egg",
                        uncertainty_cups = 0.0410032146343626f,
                        volume_cups = 0.41003214634362595f,
                    },
                    new VolumeData
                    {
                        object_name = "rice",
                        uncertainty_cups = 0.13725909535326242f,
                        volume_cups = 1.3725909535326242f
                    }
                }
                },
                macronutrients = new MacronutrientsData
                {
                    data = new List<FoodMacroData>
                {
                    new FoodMacroData
                    {
                        found = true,
                        macros = new MacroNutrients
                        {
                            calories = 596.66046f,
                            carbs = 2.2825572f,
                            fat = 42.683823f,
                            protein = 46.6783f
                        },
                        calculated_weight = 100,
                        requested_food = "egg",
                        volume = 0.41003215f
                    },
                    new FoodMacroData
                    {
                        found = true,
                        macros = new MacroNutrients
                        {
                            calories = 461.09473f,
                            carbs = 85.35013f,
                            fat = 8.330989f,
                            protein = 11.391352f
                        },
                        calculated_weight = 160,
                        requested_food = "rice",
                        volume = 1.3725909f
                    }
                }
                },
                success = true
            };
        }

        // Example usage of test data:
        public static void PrintTestData()
        {
            var response = CreateDummyResponse();
            foreach (var foodData in response.macronutrients.data)
            {
                var displayInfo = FoodNutritionInfo.FromApiData(response.data, foodData);
                Debug.Log($"Food: {displayInfo.foodName}\n" +
                         $"Volume: {displayInfo.estimatedVolume} {displayInfo.volumeUnit}\n" +
                         $"Weight: {displayInfo.calculatedWeight} {displayInfo.weightUnit}\n" +
                         $"Macros (g) - Protein: {displayInfo.protein}, Fat: {displayInfo.fat}, Carbs: {displayInfo.carbohydrates}");
            }
        }
    }
}
