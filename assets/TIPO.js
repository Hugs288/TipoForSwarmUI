postParamBuildSteps.push(() => {
    setInterval(() => {
        let TIPOParamGroup = document.getElementById('input_group_content_tipopromptgeneration');
        if (!TIPOParamGroup) {
            return; // Exit if the parent element isn't on the page
        }
        let installButton = document.getElementById('tipo_prompt_generation_install_button');
        let isInstalled = currentBackendFeatureSet.includes('tipo_prompt_generation');

        if (isInstalled && installButton) {
            // If it's installed and the button exists, remove the button.
            installButton.remove();
        }
        else if (!isInstalled && !installButton) {
            // If it's not installed and the button doesn't exist, create it.
            TIPOParamGroup.append(createDiv('tipo_prompt_generation_install_button', 'keep_group_visible', `<button class="basic-button" onclick="installFeatureById('tipo_prompt_generation', 'tipo_prompt_generation_install_button')">Install TIPO</button>`));
        }
    }, 1000);
});
