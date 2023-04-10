﻿using StableDiffusionSdk.Integrations.StableDiffusionWebUiApi;

namespace StableDiffusionSdk.StableDiffusionApi
{
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading.Tasks;
    using System;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using StableDiffusionSdk.DomainModels;

    public class StableDiffusionApi
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;

        public StableDiffusionApi(string apiUrl)
        {
            _apiUrl = apiUrl;
            _httpClient = new HttpClient();
        }

        public async Task<ImageDomainModel> ImageToImage(Img2ImgRequest request)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var apiRequest = CreateApiRequestModel(request);

            var response = await _httpClient.PostAsJsonAsync($"{_apiUrl}/sdapi/v1/img2img", apiRequest, options);

            var jsonResponse = await response.Content.ReadAsStringAsync();

            response.EnsureSuccessStatusCode();

            var img2ImgResponse = JsonSerializer.Deserialize<Img2ImageApiResponse>(jsonResponse, options)!;

            var base64String = img2ImgResponse.images.First();
            var imageBytes = Convert.FromBase64String(base64String);

            using var image = Image.Load<Rgba32>(imageBytes);

            return new ImageDomainModel(base64String, image.Width, image.Height);
        }

        public async Task<string> Interrogate(InterrogateRequest req)
        {
            var request = new InterrogateApiRequest(req.InputImage.ContentAsBase64String, 
                req.Model == InterrogationModel.Clip ? "clip": "deepdanbooru");

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var response = await _httpClient.PostAsJsonAsync($"{_apiUrl}/sdapi/v1/interrogate", request, options);

            var jsonResponse = await response.Content.ReadAsStringAsync();

            response.EnsureSuccessStatusCode();
            
            var parsedResponse = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonResponse);

            return parsedResponse!["caption"];
        }

        public Img2ImgApiRequest CreateApiRequestModel(Img2ImgRequest request)
        {
            return new Img2ImgApiRequest()
            {
                Init_images = new List<string>()
                {
                    request.InputImage.ContentAsBase64String
                },
                Cfg_scale = request.CfgScale,
                Denoising_strength = request.DenoisingStrength,
                Steps = request.Steps,
                Width = request.InputImage.Width,
                Height = request.InputImage.Height,
                Seed = request.Seed.Value,
                Prompt = request.Prompt,
                Negative_prompt = request.NegativePrompt,
                Restore_faces = request.RestoreFaces,


                Sampler_name = "Euler a",
                Resize_mode = 1,
            };
        }
    }
}