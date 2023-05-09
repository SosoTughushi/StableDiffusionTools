﻿using StableDiffusionSdk.Infrastructure;
using StableDiffusionSdk.Jobs;
using StableDiffusionSdk.Prompts;
using StableDiffusionTools.Domain;
using StableDiffusionTools.ImageUtilities;
using StableDiffusionTools.Integrations.StableDiffusionWebUi;

namespace StableDiffusionSdk.Workflows;

public class SmoothZoomInWorkflow
{
    private readonly StableDiffusionApi _stableDiffusionApi;
    private readonly IPrompter _prompter;

    public SmoothZoomInWorkflow(StableDiffusionApi stableDiffusionApi, IPrompter prompter)
    {
        _stableDiffusionApi = stableDiffusionApi;
        _prompter = prompter;
    }

    public async Task<Unit> Run(string path, int rezolution, double zoomPercent, double zoomInCount, double denoisingStrength, int middleStepCount)
    {
        var baseOutputFolder = Path.Combine(Path.GetDirectoryName(path)!,
            Path.GetFileNameWithoutExtension(path));
        var _persistor = new ImagePersister(baseOutputFolder);
        var jsonWriter = new JsonWriter(_persistor.OutputFolder);

            

        //var zoomDirection = ZoomDirectionBuilder.Right(21.3).Bottom(12.5);
        var zoomDirection = ZoomDirectionBuilder.Right(0).Bottom(0);

        var input = await path.ReadImage();

        var preview = await input.PreviewZoom(zoomPercent, zoomDirection, 40);
        await _persistor.Persist(preview);

        input = await input.Resize(rezolution);

        
        var regulator = new RgbRegulator();
        for (var recursionCount = 0; recursionCount < zoomInCount; recursionCount++)
        {
            var gptPrompt = await _prompter.GetPrompt(input);
            var seed = Seed.Random();

            var zoomDelta = zoomPercent - 100;
            var zoomDeltaEachStep = zoomDelta / middleStepCount;
            double denoisingStrengthStep = denoisingStrength / middleStepCount;

            var result = input;
            for (var i = 1; i <= middleStepCount; i++)
            {
                var zoomed = await input.Zoom(100 + zoomDeltaEachStep * i, zoomDirection);
                var regulated = await regulator.Regulate(zoomed);

                var ds = Math.Round(denoisingStrengthStep * i, 3);
                result = await Image2Image(regulated, gptPrompt, seed, jsonWriter, ds);

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