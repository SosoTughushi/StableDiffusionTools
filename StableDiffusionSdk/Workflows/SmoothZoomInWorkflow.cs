﻿using StableDiffusionSdk.Infrastructure;
using StableDiffusionSdk.Integrations.OpenAiGptApi;
using StableDiffusionSdk.Integrations.StableDiffusionWebUiApi;
using StableDiffusionSdk.Jobs;
using StableDiffusionSdk.Modules.Images;
using StableDiffusionSdk.Modules.Prompts;

namespace StableDiffusionSdk.Workflows
{
    public class SmoothZoomInWorkflow
    {
        private readonly StableDiffusionApi _stableDiffusionApi;
        private readonly GptApi _gptApi;

        public SmoothZoomInWorkflow(StableDiffusionApi stableDiffusionApi, GptApi gptApi)
        {
            _stableDiffusionApi = stableDiffusionApi;
            _gptApi = gptApi;
        }

        public async Task<Unit> Run(string path, int rezolution, double zoomPercent)
        {
            var baseOutputFolder = Path.Combine(Path.GetDirectoryName(path)!,
                Path.GetFileNameWithoutExtension(path));
            var _persistor = new ImagePersister(baseOutputFolder);
            var jsonWriter = new JsonWriter(_persistor.OutputFolder);

            string? prompt = null!;
            var promptCount = 0;

            async Task<string> CreatePrompt(
                ImageDomainModel image)
            {
                promptCount++;
                if (promptCount % 10 == 0 || prompt is null)
                {
                    var clip = await _stableDiffusionApi.InterrogatePlease(image, InterrogationModel.Clip);

                    var randomPrompt = new[]
                    {
                        "van gogh",
                        "steampunk",
                        "blade runner thematics",
                        "scene from animatrix",
                        "replace everything with cyberpunk",
                        "bored cosmic entity looking at camera",
                    };

                    var styles = new[]
                    {
                        "charliebo artstyle",
                        "holliemengert artstyle",
                        "marioalberti artstyle",
                        "pepelarraz artstyle",
                        "andreasrocha artstyle",
                        "jamesdaly artstyle",
                        "comicmay artsyle"
                    };

                    prompt = await _gptApi.Consolidate(
                        @$"{randomPrompt.PickRandom()}, trigger word: [{styles.PickRandom()}]",
                        clip);
                }

                return prompt;
            }

            var zoomDirection = ZoomDirectionBuilder.Right(21.3).Bottom(12.5);

            var input = await path.ReadImage();

            var preview = await input.PreviewZoom(zoomPercent, zoomDirection, 40);
            await _persistor.Persist(preview);

            input = await input.Resize(rezolution);


            const double denoisingStrength = 0.25;
            const int recursionSteps = 40;
            const int inBetweenSteps = 5;
            var regulator = new RgbRegulator();
            for (var recursionCount = 0; recursionCount < recursionSteps; recursionCount++)
            {
                var gptPrompt = await CreatePrompt(input);
                var seed = Seed.Random();

                var zoomDelta = zoomPercent - 100;
                var zoomDeltaEachStep = zoomDelta / inBetweenSteps;
                const double denoisingStrengthStep = denoisingStrength / inBetweenSteps;

                var result = input;
                for (var i = 1; i <= inBetweenSteps; i++)
                {
                    var zoomed = await input.Zoom(100 + zoomDeltaEachStep * i, zoomDirection);
                    var regulated = await regulator.Regulate(zoomed);
                    result = await Image2Image(regulated, gptPrompt, seed, jsonWriter, denoisingStrengthStep * i);

                    await _persistor.Persist(result);
                }

                input = result;
            }

            return new Unit();
        }

        private async Task<ImageDomainModel> Image2Image(ImageDomainModel input, string gptPrompt, Seed seed,
            JsonWriter jsonWriter, double denoisingStrength)
        {
            var request = new Img2ImgRequest(
                InputImage: input,
                Prompt: gptPrompt,
                DenoisingStrength: denoisingStrength,
                NegativePrompt: "",
                Seed: seed
            );

            await jsonWriter.Write(request);
            var generated = await _stableDiffusionApi.ImageToImage(request);
            return generated;
        }
    }
}