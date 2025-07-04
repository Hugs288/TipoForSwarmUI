postParamBuildSteps.push(() => {
    let TIPOParamGroup = document.getElementById('input_group_content_tipopromptgeneration');
    if (TIPOParamGroup && !currentBackendFeatureSet.includes('tipo_prompt_generation')) {
        TIPOParamGroup.append(createDiv(`tipo_prompt_generation_install_button`, 'keep_group_visible', `<button class="basic-button" onclick="installFeatureById('tipo_prompt_generation', 'tipo_prompt_generation_install_button')">Install TIPO</button>`));
    }
});
