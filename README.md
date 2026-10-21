# Classroom-Anomaly (Marcia VR Research Project)

A virtual reality (VR) classroom project built with **Unity 6**. This guide walks you through downloading the project to your own computer and opening it, step by step.

---

## Table of Contents

1. [What is this project?](#1-what-is-this-project)
2. [Before you start: what you need](#2-before-you-start-what-you-need)
3. [Step 1: Create a free GitHub account](#3-step-1-create-a-free-github-account)
4. [Step 2: Install GitHub Desktop](#4-step-2-install-github-desktop)
5. [Step 3: Download (clone) the project](#5-step-3-download-clone-the-project)
6. [Step 4: Install Unity Hub and Unity 6](#6-step-4-install-unity-hub-and-unity-6)
7. [Step 5: Open the project in Unity](#7-step-5-open-the-project-in-unity)
8. [Step 6: Open the classroom scene and press Play](#8-step-6-open-the-classroom-scene-and-press-play)
9. [Using a VR headset (optional)](#9-using-a-vr-headset-optional)
10. [Getting updates later](#10-getting-updates-later)
11. [Troubleshooting](#11-troubleshooting)
12. [For team members who want to change the project](#13-for-team-members-who-want-to-change-the-project)

---

## 1. What is this project?

This is a **Unity project**. Unity is a free program used to build 3D games and VR experiences. The project contains a virtual classroom, including a student's VR setup and non-player characters (NPCs, which are computer-controlled people in the scene).

You do **not** need to know how to code to open it and look around. You only need to download it and open it in Unity.

---

## 2. Before you start: what you need

| Requirement | Details |
|---|---|
| **A computer** | Windows 10/11 or macOS. A computer with a dedicated graphics card is strongly recommended, since VR projects are demanding. |
| **Free disk space** | **At least 20 GB** (the project, Unity itself, and temporary files all take space). |
| **Internet connection** | You will download several large files. |
| **Time** | About 1 to 2 hours the first time. Most of that is waiting for downloads and for Unity to prepare the project. |
| **A VR headset** | *Optional.* You can look at the project on your computer screen without one. See [Section 9](#9-using-a-vr-headset-optional). |

---

## 3. Step 1: Create a free GitHub account

GitHub is the website where this project is stored.

1. Go to **https://github.com** and click **Sign up**.
2. Enter your email, create a password, and pick a username.
3. Verify your email address when GitHub asks.

> **Tip:** Since this is a team project, send your GitHub username to the project lead so you can be added to the team if you need to make changes. If you only want to download and view the project, you do not need to be added.

---

## 4. Step 2: Install GitHub Desktop

**GitHub Desktop** is a free app that downloads projects from GitHub for you with a few clicks. You do not have to type any commands.

1. Go to **https://desktop.github.com**.
2. Click the download button for your computer (Windows or macOS).
3. Open the downloaded file and follow the installer's prompts.
4. Open GitHub Desktop. When it asks you to sign in, choose **Sign in to GitHub.com** and use the account from Step 1.
5. If it asks for a name and email for "Git configuration", use your real name and the email on your GitHub account. Click **Finish**.

---

## 5. Step 3: Download (clone) the project

1. In GitHub Desktop, click **File** then **Clone repository…**
2. Click the **URL** tab.
3. In the first box, paste this address:

   ```
   https://github.com/Marcia-VR-Project-Team/Marcia-VR-Research-Project
   ```

4. In the **Local path** box, choose where the project will be saved. Pick a simple location, for example:
   - Windows: `C:\Projects`
   - Mac: your `Documents` folder

   > ⚠️ **Avoid** very long folder paths, folders with unusual characters, and folders that sync to the cloud (such as OneDrive, Dropbox, or iCloud Drive). Syncing can corrupt Unity projects.

5. Click **Clone**.
6. Wait for the download to finish. It may take several minutes. A progress bar will show.

When it is done, you have a folder called `Marcia-VR-Research-Project` on your computer. Inside you will see folders named `Assets`, `Packages`, and `ProjectSettings`. Those are the project's files.

### Which version of the project do I get?

The project has more than one **branch**. By default you will get the **`main`** branch, which is correct for most people.

The team also uses a branch called **`dev`**, where everyone's work is combined. If someone on the team tells you to use `dev`, click the **Current Branch** button at the top of GitHub Desktop and select `dev`.

### Alternative: download a ZIP file (not recommended)

On the project's GitHub page you can click the green **Code** button and then **Download ZIP**. This works for a one-time look, but it will not let you receive updates later, and large files may be missing. Using GitHub Desktop is the better option.

---

## 6. Step 4: Install Unity Hub and Unity 6

**Unity Hub** is a launcher that manages Unity. This project needs **Unity 6**.

### 6a. Install Unity Hub

1. Go to **https://unity.com/download**.
2. Download **Unity Hub** for your computer and install it.
3. Open Unity Hub and **sign in** (or create a free Unity account). You will be asked to accept a license. Choose the **Personal** (free) license if you are eligible.
3. In Unity Hub, click **Installs** in the left menu, then **Install Editor**.
4. If the exact version is not listed, click **Archive** and then the **download archive** link, find the matching version, and click **Install** (Unity Hub will open).
5. When asked which **modules** to add, check these:
   - **Windows Build Support** or **Mac Build Support** (whichever matches your computer)
   - **Android Build Support** (only if your VR headset is a standalone headset such as a Meta Quest, which runs Android). Include its sub-items **OpenJDK** and **Android SDK & NDK Tools** if offered.
6. Click **Install** and wait. This is a large download (several GB).

---

## 7. Step 5: Open the project in Unity

1. Open **Unity Hub** and click **Projects** in the left menu.
2. Click **Add** then **Add project from disk**.
3. Browse to the folder you cloned (`Marcia-VR-Research-Project`) and select it. Click **Add Project** / **Select Folder**.
4. The project now appears in your list. Click it to open it.
5. If Unity asks which editor version to use, choose the one you installed in Step 4.
6. **Be patient.** The first time you open the project, Unity has to import every file and prepare everything. This can take **10 to 30 minutes or more**. You will see a progress bar. Do not close the window.
7. If a window appears saying **"Enter Safe Mode?"** because of script errors, click **Ignore** (or **Enter Safe Mode** and let it finish). If this keeps happening, see [Troubleshooting](#11-troubleshooting).

When it finishes, you will see the Unity editor: a large window with panels for the scene, hierarchy, project files, and more.

---

## 8. Step 6: Open the classroom scene and press Play

This project's main scene is the classroom.

1. At the bottom of the Unity window, find the **Project** panel (it shows folders).
2. Open the folder **Assets** → **Scenes**.
3. Double-click the scene named **Classroom**.
4. The classroom appears in the large **Scene** view. You can look around:
   - **Hold the right mouse button** and move the mouse to look around.
   - While holding the right mouse button, use **W, A, S, D** to move.
   - **Scroll the mouse wheel** to zoom.
5. To run the experience, click the **Play ▶** button at the top center of the window. Click it again to stop.

> **Note:** If you are not wearing a VR headset, running the scene on your monitor may look different from the headset experience, and controllers will not work. That is normal.

---

## 9. Using a VR headset (optional)

How you connect a headset depends on which one you have. Follow the instructions from your headset's maker for connecting it to a computer, then try Play in Unity.

**Headsets that connect to a PC** (for example Meta Quest with Link or Air Link, Valve Index, HTC Vive):
1. Install the headset's PC software (Meta Quest Link, SteamVR, etc.) and connect the headset.
2. Make sure the headset software is running and shows the headset is connected.
3. In Unity, press **Play ▶**. The classroom should appear in your headset.

**Standalone headsets** (for example Meta Quest 2/3): you can use Link/Air Link to connect to your computer as above. Building an installable app for the headset requires Android Build Support (see Step 4) and is a more advanced task, so ask the project lead for help.

> 🔧 *Project lead: if the project requires specific headset settings (such as OpenXR plugins or a specific headset model), list them here.*

---

## 10. Getting updates later

The team may update the project over time. To receive the latest changes:

1. **Close Unity** completely.
2. Open **GitHub Desktop**.
3. Make sure the correct project is selected at the top-left ("Current Repository") and the correct branch is selected ("Current Branch").
4. Click **Fetch origin**. If the button changes to **Pull origin**, click it.
5. Wait for it to finish, then open the project in Unity again.

If you have made your own changes and Pull shows a conflict or error, stop and ask the project lead before continuing.

---

## 11. Troubleshooting

**"Unity asks me to upgrade or change the project version."**
Cancel and install the exact version listed in `ProjectSettings/ProjectVersion.txt` (see Step 4b).

**"Unity says there are compiler errors" or shows a Safe Mode prompt.**
- Choose **Ignore** and give it time to finish importing.
- Close Unity and reopen the project. The first import sometimes needs a second try.
- Check you have a working internet connection. Unity downloads some required packages the first time.

**"The project is taking forever to open."**
This is normal the first time (up to 30+ minutes on slower computers). If the progress bar has not moved for over an hour, close Unity and reopen the project.

**"Everything is pink or the colors look wrong."**
This usually means graphics materials have not finished loading. Wait for importing to finish and reopen the scene. If it persists, ask the project lead.

**"Some files or textures are missing, or I see tiny text files where models should be."**
Large files in this kind of project are sometimes stored with a tool called **Git LFS**. GitHub Desktop normally handles this for you. If you see the problem, install Git LFS from **https://git-lfs.com**, restart GitHub Desktop, then use **Repository** → **Pull** to fetch the files again.

**"I don't see the Classroom scene."**
Make sure you are on the correct branch (`main` or `dev`) in GitHub Desktop, and that the clone finished completely. Then look in **Assets → Scenes** in Unity's Project panel.

**"My computer runs slowly or Unity crashes."**
Close other programs, make sure you have enough free disk space (20 GB or more), and make sure your graphics drivers are up to date.

**Still stuck?**
Contact the project team (for example on the team Discord) and tell them: your operating system, your Unity version, and what the error message says (a screenshot helps a lot).

---

## 12. For team members who want to change the project

If you have been asked to contribute, the team follows these rules to avoid breaking each other's work.

**General rules**
- Make **prefabs** out of your work whenever possible (for example, the UI is a prefab, each NPC is a prefab, and the student's VR rig is a prefab).

**Saving your changes to your own branch**
1. Make sure your changes are complete and ready to be saved.
2. In Unity, **Save all** (File → Save, and File → Save Project).
3. **Close Unity.**
4. In GitHub Desktop, **Fetch** your branch and `dev`.
5. Check regularly whether anyone has pushed to `dev`. If they have, **merge `dev` into your branch** so you have everyone's latest work.
6. **Commit** to your branch, but **do not push yet**.
7. If there are merge conflicts, keep the changes from **your** branch and scene.
8. Make sure you are committing the most up-to-date prefabs (use **Override** in Unity if you changed a prefab).
9. **Push** to your branch.

**If your prefab(s) are already in the `dev` scene**
1. Switch to `dev` and merge your branch into `dev`.
2. Push to `dev`.

**If your prefab(s) have not been placed in the `dev` scene yet**
1. After pushing your branch, switch to `dev`.
2. Merge your branch into `dev` and **push**. If there are merge conflicts, keep the version from **your branch**. If you are unsure, message the **@Marcia** role on Discord.
3. Place your prefabs **in the Hierarchy** of the `dev` branch's scene at **Assets → Scenes → Classroom**.
4. Save in Unity, then **close Unity**.
5. Make sure you are on the `dev` branch.
6. **Commit**, then **push**.

> 📣 **Always notify the @Marcia role on Discord when you push to `dev`.**

---

*Built with Unity 6. Forked from [KareO2/Classroom-Anomaly](https://github.com/KareO2/Classroom-Anomaly).*
