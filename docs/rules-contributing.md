# Contribution Standards

Hello! So you've decided to contribute to this project. Happy to have you join us here! This document outlines the contribution standards of this project. These standards help make things run smoothly, particularly in merging contributions into the project.

Contributions that do not meet these standards may not be merged until the relevant issues have been addressed. If you have any questions regarding our contribution standards, feel free to ask in [#Dev-Help](https://discord.com/channels/1087916308160585740/1524110310645043291) on our [discord](https://discord.gg/DScJKwwaZ).

## Requirements
All contributions must meet the following requirements. Repeated, intentional, or egregious violation of any of these policies may result in a temporary or permanent ban on contributing to the project.

1. **Zero Tolerance for Plagiarism** - All contributions must be entirely original work or credit the original source(s). GitHub commits contain information on the original source(s) and are generally sufficient to meet this requirement. If you are questioning whether you should credit someone, the answer is *always* yes.

2. **Copyright Licensing** - All code in this project must be licensed under an MIT license found [here](LICENSE.TXT). Content from other servers may only be used if the content is also licensed under an equivalent license. Custom assets are to be licensed under CC-BY-SA 3.0. Assets from other servers may only be used if the assets are licensed under an equivalent license.

3. **AI Disclosure** - Any contributions made using, in part or in full, any generative AI in the writing, debugging, or creation of content must disclose that use within the pull request for the contribution.

4. **Testing Standards** - All contributions should be tested thoroughly prior to submitting the PR for review. Pictures and videos in the *media* section of the PR template are a great way to prove testing. ***Developers*** will review and test the PR prior to merging, but it is the contributor's responsibility to ensure they are complete prior to merging.

    Unique to our project is the concern of saving. All PRs submitted to the Persistence_Testing branch should be tested as safe for saving and loading.


5. **Follow Guidelines** - All contributors should understand and aim to follow the guidelines listed below. Failing to meet the guidelines is not a violation of our contribution requirements, but may result in the pull request being closed or changes requested prior to accepting the changes. Repeated or intentional disregard for guidelines may be considered a requirement violation.


## Guidelines

1. **PR Template** - A PR template is available for making new pull requests. All relevant sections of this PR template should be included in any contribution to the project including:
    * A description of the change
    * Balance justification
    * Technical details
    * Relevant Media
    * Breaking Changes
    * Changelog
    
    Some sections may not be necessary (i.e. the media section on a change to a number in the code). Any unnecessary sections may be omitted from the PR. PRs may choose to not use the PR template provided all relevant sections above are included.

2. **Project Folders** - All new items (files, prototypes, sound files, etc) should be stored in the designated *_Persistence14* folders within their relevant codespace. All new prototypes should be stored in a new file, even if similar in kind to existing prototypes. Project folders should, where possible, mirror the folder structure of the main folders they are contained within. 

    For instance, adding a new script relevant to *Content.Shared/Atmos*, it should be stored in *Content.Shared/_Persistence14/Atmos*

3. **Modifying Files** - Sometimes we need to make changes to existing files instead of creating new ones. In such cases, the changes should be marked with a comment identifying where the change was made. A simple "// Modified for Persistence14" is sufficient.

4. **Discord Discussion** - Major changes should be discussed on Discord prior to being implemented into a PR. The [#Suggestion-Threads](https://discord.com/channels/1087916308160585740/1434631126043070605) channel is the ideal place to describe your ideas and talk them out with people.

5. **Keep PRs Focused** - A PR should address one feature/bug/balance concern. If you would like to fix a bug in a system, and then update that system, those should be two separate PRs.

## Discussion Rules
Whether asking questions about a PR, providing feedback, or offering suggestions, all discussion within regarding contributions (whether in Discord, GitHub, or elsewhere) must abide by the Discord rules found [here](rules-discord.md).