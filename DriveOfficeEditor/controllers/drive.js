const fs = require("fs");
const axios = require("axios");
const FormData = require("form-data");
const mime = require("mime-types");
const path = require("path");
const metadataService = require("../services/metadataService");
const logger = require("../services/logger.js");
const { config } = require("../config/config.js");

exports.uploadNewFileToDrive = async (req, res, next) => {
  const xTypes = Object.values(config.xTypes);
  if (!req.query.name || !req.query.type) {
    logger.log({
      level: "error",
      message: `Status 400: name and type are required to create a blank file`,
      label: `user: ${req.user.id}`,
    });
    return res.status(400).send("Status 400: name and type are required to create a blank file");
  }
  req.query.type = req.query.type.toLowerCase();
  if (!xTypes.includes(req.query.type)) {
    return res.status(400).send("status 400: type must be docx,pptx, or xlsx!");
  }
  const blankFilePath = `${process.env.BLANK_PATH}/blank.${req.query.type}`;
  const newFilePath = `${process.env.TEMPLATE_FOLDER}/${req.query.name}.${req.query.type}`;
  fs.copyFileSync(blankFilePath, newFilePath);
  const size = fs.statSync(newFilePath).size;
  const data = new FormData();
  data.append("file", fs.createReadStream(newFilePath));
  fs.unlinkSync(newFilePath);
  const accessToken = metadataService.getAuthorizationHeader(req.user);
  let fileId;
  try {
    fileId = await upload(data, { parent: req.query.parent, name: req.query.name, size }, accessToken);
    res.locals.fileId = fileId;
    next();
  } catch (error) {
    if (error.response.status == 400) {
      logger.log({
        level: "error",
        message: "status 400: name of newFile is already taken",
        label: `user: ${req.user.id}`,
      });
      return res.status(400).send("status 400: name of newFile is already taken");
    } else {
      logger.log({
        level: "error",
        message: `status 500: ${error.message} `,
        label: `user: ${req.user.id} `,
      });
      return res.status(500).send(`status 500: ${error.message} `);
    }
  }
};

exports.redirectToDriveDownload = (req, res, next) => {
  try {
    return res.redirect(`${process.env.DRIVE_URL}/api/fs/file/${req.params.id}/download`);
  } catch {
    return res.status(500).send("error");
  }
};

async function upload(filedata, params, accessToken) {
  try {
    const queryString = Object.entries({ ...params, public: false, client: process.env.CLIENT_NAME })
      .map(([key, value]) => `${key}=${value}`)
      .join("&");
    const response = await axios({
      method: "post",
      url: `${process.env.DRIVE_URL}/api/fs/file?${queryString}`,
      headers: {
        ...accessToken,
        ...filedata.getHeaders(),
        "content-type": "multipart/form-data",
      },
      data: filedata,
    });
    return response.data;
  } catch (error) {
    throw error;
  }
}

exports.updateFile = async (fileId, filePath, accessToken) => {
  try {
    const size = getFileSize(filePath);
    mimeType = mime.contentType(path.extname(filePath));
    const data = new FormData();
    data.append("file", fs.createReadStream(filePath));
    const updateRequest = {
      method: "post",
      url: `${process.env.DRIVE_URL}/api/files/${fileId}/reupload?size=${size}`,
      headers: {
        ...accessToken,
        ...data.getHeaders(),
        "content-type": "multipart/form-data",
      },
      data: data,
    };
    await axios(updateRequest);
  } catch (error) {
    throw error;
  }
};

exports.downloadFileFromDrive = async (idToDownload, downloadedFilePath, accessToken) => {
  try {
    const writer = fs.createWriteStream(downloadedFilePath);
    const downloadRequest = {
      method: "GET",
      url: `${process.env.DRIVE_URL}/api/fs/file/${idToDownload}/download`,
      headers: {
        ...accessToken,
      },
      responseType: "stream",
    };
    const response = await axios(downloadRequest);
    response.data.pipe(writer);
    return new Promise((resolve, reject) => {
      writer.on("finish", resolve);
      writer.on("error", reject);
    });
  } catch (error) {
    throw error;
  }
};

function getFileSize(filePath) {
  try {
    const stats = fs.statSync(filePath);
    return `${stats.size}`;
  } catch (error) {
    throw error;
  }
}
